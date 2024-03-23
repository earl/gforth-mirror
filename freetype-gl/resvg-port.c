/****************************************************************************
 *
 * rsvg-port.c
 *
 *   Libresvg-based hook functions for OT-SVG rendering in FreeType
 *   (implementation).
 *
 * Copyright (C) 2022-2024 by
 * David Turner, Robert Wilhelm, Werner Lemberg, and Moazin Khatti.
 *
 * This file is part of the FreeType project, and may only be used,
 * modified, and distributed under the terms of the FreeType project
 * license, LICENSE.TXT.  By continuing to use, modify, or distribute
 * this file you indicate that you have read the license and
 * understand and accept it fully.
 *
 */

#include <ft2build.h>
#include <freetype/otsvg.h>

#ifdef HAVE_LIBRESVG

#include <resvg.h>
#include <stdlib.h>
#include <math.h>
#include <time.h>

#include <freetype/freetype.h>
#include <freetype/ftbbox.h>

#include "resvg-port.h"


  /*
   * The init hook is called when the first OT-SVG glyph is rendered.  All
   * we do is to allocate an internal state structure and set the pointer in
   * `library->svg_renderer_state`.  This state structure becomes very
   * useful to cache some of the results obtained by one hook function that
   * the other one might use.
   */
  FT_Error
  resvg_port_init( FT_Pointer  *state )
  {
    /* allocate the memory upon initialization */
    *state = malloc( sizeof( Resvg_Port_StateRec ) ); /* XXX error handling */
    bzero(state, sizeof( Resvg_Port_StateRec ) );

    return FT_Err_Ok;
  }


  /*
   * Deallocate the state structure.
   */
  void
  resvg_port_free( FT_Pointer  *state )
  {
    resvg_tree_destroy(((Resvg_Port_StateRec*)state)->tree);
    free( *state );
  }


  /*
   * The render hook.  The job of this hook is to simply render the glyph in
   * the buffer that has been allocated on the FreeType side.  Here we
   * simply use the recording surface by playing it back against the
   * surface.
   */
  FT_Error
  resvg_port_render( FT_GlyphSlot  slot,
		     FT_Pointer   *_state )
  {
    /* FreeType variables. */
    FT_Error  error = FT_Err_Ok;
    FT_SVG_Document  document = (FT_SVG_Document)slot->other;
    FT_Size_Metrics  metrics  = document->metrics;
    FT_UShort  units_per_EM   = document->units_per_EM;
    FT_UShort  end_glyph_id   = document->end_glyph_id;
    FT_UShort  start_glyph_id = document->start_glyph_id;
    char  *id;
    char  str[32];
    
    if ( start_glyph_id < end_glyph_id )
    {
      /* Render only the element with its ID equal to `glyph<ID>`. */
      snprintf( str, sizeof(str), "glyph%u", slot->glyph_index );
      id = str;
      fprintf(stderr, "id=%s\n", id);
    }
    else
    {
      /* NULL = Render the whole document */
      id = NULL;
      fprintf(stderr, "#id=%d\n", start_glyph_id);
    }

    /* Librsvg variables. */
    /* General variables. */

    resvg_render_tree *tree;
    resvg_transform transform;
    FT_Matrix ft_transform = document->transform;
    FT_Vector ft_delta = document->delta;

    if(id) {
      if(resvg_get_node_transform(((Resvg_Port_StateRec*)_state)->tree, id,
				  &transform)) {
	transform.a*=metrics.x_scale/4194304.;
	transform.b*=metrics.y_scale/4194304.;
	transform.c*=metrics.x_scale/4194304.;
	transform.d*=metrics.y_scale/4194304.;
	transform.e*=metrics.x_scale/4194304.;
	transform.f*=metrics.y_scale/4194304.;
	resvg_render_node(((Resvg_Port_StateRec*)_state)->tree,
			  id,
			  transform,
			  (int)slot->bitmap.width,
			  (int)slot->bitmap.rows,
			  slot->bitmap.buffer);
      }
    } else {
      transform=resvg_transform_identity();
      transform.a*=metrics.x_scale/4194304.;
      transform.b*=metrics.y_scale/4194304.;
      transform.c*=metrics.x_scale/4194304.;
      transform.d*=metrics.y_scale/4194304.;
      transform.e*=metrics.x_scale/4194304.;
      transform.f*=metrics.y_scale/4194304.;
      resvg_render(((Resvg_Port_StateRec*)_state)->tree,
		   transform,
		   (int)slot->bitmap.width,
		   (int)slot->bitmap.rows,
		   slot->bitmap.buffer);
    }
    fprintf(stderr,
	    "transform: %f %f %f matrix: %f %f delta: %f\n"
	    "           %f %f %f         %f %f        %f\n",
	    transform.a, transform.c, transform.e, ft_transform.xx/65536., ft_transform.xy/65536., ft_delta.x/65536.,
	    transform.b, transform.d, transform.f, ft_transform.yx/65536., ft_transform.yy/65536., ft_delta.y/65536.);

    int i;
    uint32_t* ptr=(uint32_t*)(slot->bitmap.buffer);
    
    for(i=0; i < (int)slot->bitmap.width * (int)slot->bitmap.rows; i++, ptr++) {
      uint32_t bgra, rgba;
      bgra = *ptr;
#if __BYTE_ORDER == __BIG_ENDIAN
      rgba = rol(__builtin_bswap32(bgra), 8);
#else
      rgba = rol(__builtin_bswap32(bgra), 24);
#endif
      *ptr = rgba;
    }
    return error;
  }


  /*
   * This hook is called at two different locations.  Firstly, it is called
   * when presetting the glyphslot when `FT_Load_Glyph` is called.
   * Secondly, it is called right before the render hook is called.  When
   * `cache` is false, it is the former, when `cache` is true, it is the
   * latter.
   *
   * The job of this function is to preset the slot setting the width,
   * height, pitch, `bitmap.left`, and `bitmap.top`.  These are all
   * necessary for appropriate memory allocation, as well as ultimately
   * compositing the glyph later on by client applications.
   */
  FT_Error
  resvg_port_preset_slot( FT_GlyphSlot  slot,
			  FT_Bool       cache,
			  FT_Pointer   *_state )
  {
    FT_Error  error = FT_Err_Ok;
    FT_SVG_Document  document = (FT_SVG_Document)slot->other;
    FT_Size_Metrics  metrics  = document->metrics;
    FT_UShort  end_glyph_id   = document->end_glyph_id;
    FT_UShort  start_glyph_id = document->start_glyph_id;
    FT_Matrix ft_transform = document->transform;
    FT_Vector ft_delta = document->delta;
    resvg_options *opts=resvg_options_create();
    resvg_rect imgrect;
    double tmpd;
    char  *id;
    char  str[32];
    Resvg_Port_StateRec* state = (Resvg_Port_StateRec*)_state;
    
    if ( start_glyph_id < end_glyph_id ) {
      /* Render only the element with its ID equal to `glyph<ID>`. */
      snprintf( str, sizeof(str), "glyph%u", slot->glyph_index );
      id = str;
      fprintf(stderr, "preset id=%s∈[%d,%d]\n", id, start_glyph_id, end_glyph_id);
    } else {
      /* NULL = Render the whole document */
      id = NULL;
      fprintf(stderr, "preset #id=%d\n", start_glyph_id);
    }

    fprintf(stderr,
	    "Metrics: x/y ppem=     %f %f\n"
	    "         x/y scale=    %f %f\n"
	    "         a/de scender= %f %f\n"
	    "         heigth=       %f\n"
	    "         max_advance=  %f\n",
	    metrics.x_ppem/64., metrics.y_ppem/64.,
	    metrics.x_scale/65536., metrics.y_scale/65536.,
	    metrics.ascender/64., metrics.descender/64.,
	    metrics.height/64., metrics.max_advance/64.);

    if((state->tree == NULL) ||
       (state->start_id != start_glyph_id) ||
       (state->end_id != end_glyph_id)) {
      if(state->tree) {
	resvg_tree_destroy(state->tree);
	state->tree = NULL;
      }
      /* Form an `resvg_render_tree` by loading the SVG document. */
      struct timespec time1, time2;
      clock_gettime(CLOCK_REALTIME,&time1);
      if( resvg_parse_tree_from_data( document->svg_document,
				      document->svg_document_length,
				      opts,
				      &((Resvg_Port_StateRec*)_state)->tree) )
	{
	  error = FT_Err_Invalid_SVG_Document;
	  goto CleanLibresvg;
	}
      state->start_id = start_glyph_id;
      state->end_id = end_glyph_id;

      clock_gettime(CLOCK_REALTIME,&time2);
      fprintf(stderr, "tree parsing time: %f\n",
	      (time2.tv_sec+time2.tv_nsec*1e-9)-
	      (time1.tv_sec+time1.tv_nsec*1e-9));
    }

    if(id) {
      resvg_get_node_bbox(((Resvg_Port_StateRec*)_state)->tree, id, &imgrect);
    } else {
      resvg_get_image_bbox(((Resvg_Port_StateRec*)_state)->tree, &imgrect);
    }

    fprintf(stderr, "BBox: %f %f %f %f\n", imgrect.x, imgrect.y, imgrect.width, imgrect.height);
    /* Preset the values. */
    slot->bitmap_left = (FT_Int) imgrect.x*metrics.x_scale/4194304.;  /* XXX rounding? */
    slot->bitmap_top  = (FT_Int)-imgrect.y*metrics.y_scale/4194304.;
    /* Do conversion in two steps to avoid 'bad function cast' warning. */
    tmpd               = ceil( imgrect.height*metrics.y_scale/4194304. );
    slot->bitmap.rows  = (unsigned int)tmpd;
    tmpd               = ceil( imgrect.width*metrics.x_scale/4194304. );
    slot->bitmap.width = (unsigned int)tmpd;
    slot->bitmap.pitch = (unsigned int)slot->bitmap.width * 4;
    slot->bitmap.pixel_mode = FT_PIXEL_MODE_BGRA;

  CleanLibresvg:
    resvg_options_destroy(opts);
    
    return error;
  }


  SVG_RendererHooks  resvg_hooks = {
                       (SVG_Lib_Init_Func)resvg_port_init,
                       (SVG_Lib_Free_Func)resvg_port_free,
                       (SVG_Lib_Render_Func)resvg_port_render,
                       (SVG_Lib_Preset_Slot_Func)resvg_port_preset_slot
                     };

#else /* !HAVE_LIBRESVG */

  SVG_RendererHooks  resvg_hooks = { NULL, NULL, NULL, NULL };

#endif /* !HAVE_LIBRESVG */


/* End */
