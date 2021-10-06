\ freetype GL helper stuff

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2014,2016,2017,2018,2019,2020 Free Software Foundation, Inc.

\ This file is part of Gforth.

\ Gforth is free software; you can redistribute it and/or
\ modify it under the terms of the GNU General Public License
\ as published by the Free Software Foundation, either version 3
\ of the License, or (at your option) any later version.

\ This program is distributed in the hope that it will be useful,
\ but WITHOUT ANY WARRANTY; without even the implied warranty of
\ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
\ GNU General Public License for more details.

\ You should have received a copy of the GNU General Public License
\ along with this program. If not, see http://www.gnu.org/licenses/.

\ freetype stuff

require unix/freetype_gl.fs
require unix/harfbuzz.fs

also freetype-gl
also opengl

\ If you want to see warnings, uncomment this:
\ 1 freetype_gl_warnings l!

' FTGL_Error_String FTGL_ERR_MAX 1+ exceptions
>r : ?ftgl-ior ( addr -- addr )
    dup 0= IF  [ r> ]L freetype_gl_errno - throw  THEN ;

ctx 0= [IF]  window-init  [THEN]

$200 Value atlas#
$200 Value atlas-bgra#

0 Value atlas
0 Value atlas-bgra
tex: atlas-tex
tex: atlas-tex-bgra \ for color emojis, actually flipped to RGBA

: init-atlas
    atlas#      dup 1 texture_atlas_new to atlas
    atlas-bgra# dup 4 texture_atlas_new to atlas-bgra
    atlas-tex      current-tex atlas      texture_atlas_t-id l!
    atlas-tex-bgra current-tex atlas-bgra texture_atlas_t-id l! ;

init-atlas

Variable fonts[] \ stack of used fonts

[IFDEF] texture_font_default_mode
    MODE_FREE_CLOSE texture_font_default_mode
[THEN]

[IFDEF] texture_font_t-scaletex
    Create texscale-xy0 1e sf, 1e sf,
    Create texscale-xy1 1e sf, 1e sf,
    Create texscale-xy2 1e sf, 1e sf,
    Create texscale-xy3 1e sf, 1e sf,
    
    : scaletex ( atlas dest -- dest ) >r
	1e dup texture_atlas_t-height @ fm/
	1e     texture_atlas_t-width  @ fm/
	r@ sf!+ sf! r> ;
    : atlas-scaletex ( -- )
	atlas texscale-xy3 scaletex set-texscale3 ;
    : atlas-bgra-scaletex ( -- )
	atlas-bgra texscale-xy2 scaletex set-texscale2 ;
[THEN]

: open-font ( atlas rfontsize addr u -- font )
    r/o map-file-private texture_font_new_from_memory
    0 over texture_font_t-scaletex
    [ sizeof texture_font_t-scaletex 4 = ] [IF] l! [THEN]
    [ sizeof texture_font_t-scaletex 2 = ] [IF] w! [THEN]
    [ sizeof texture_font_t-scaletex 1 = ] [IF] c! [THEN] ;

' texture_font_clone alias clone-font ( rfontsize font -- font )

: alpha/rgba ( atlas -- )
    GL_RGBA GL_ALPHA rot texture_atlas_t-depth @ 4 = select ;
: upload-atlas-tex ( atlas -- ) >r
    GL_TEXTURE_2D 0 r@ alpha/rgba
    r@ texture_atlas_t-width @   r@ texture_atlas_t-height @
    0 r@ alpha/rgba GL_UNSIGNED_BYTE
    r@ texture_atlas_t-data @ glTexImage2D rdrop ;
: gen-atlas-tex ( -- )
    atlas-tex
    GL_TEXTURE_2D atlas texture_atlas_t-id l@ glBindTexture edge linear
    atlas upload-atlas-tex ;
: gen-atlas-tex-bgra ( -- )
    atlas-tex-bgra
    GL_TEXTURE_2D atlas-bgra texture_atlas_t-id l@ glBindTexture edge linear
    atlas-bgra upload-atlas-tex ;

:noname defers reload-textures
    gen-atlas-tex gen-atlas-tex-bgra ; is reload-textures

\ render font into vertex buffers

2 sfloats buffer: penxy
FVariable color 0e color f!
color f@ FValue xy-color
1e FValue x-scale
1e FValue y-scale
1e FValue f-scale

: s0t0>st ( si ti addr -- ) dup     l@ t.s l!  4 + l@ t.t l! ;
: s1t0>st ( si ti addr -- ) dup 8 + l@ t.s l!  4 + l@ t.t l! ;
: s0t1>st ( si ti addr -- ) dup     l@ t.s l! 12 + l@ t.t l! ;
: s1t1>st ( si ti addr -- ) dup 8 + l@ t.s l! 12 + l@ t.t l! ;
: s0t0>st- ( si ti addr -- ) dup sf@ 75% f* dup 8 + sf@ 25% f* f+ t.s sf!  4 + l@ t.t l! ;
: s1t0>st- ( si ti addr -- ) dup sf@ 25% f* dup 8 + sf@ 75% f* f+ t.s sf!  4 + l@ t.t l! ;
: s0t1>st- ( si ti addr -- ) dup sf@ 75% f* dup 8 + sf@ 25% f* f+ t.s sf!  12 + l@ t.t l! ;
: s1t1>st- ( si ti addr -- ) dup sf@ 25% f* dup 8 + sf@ 75% f* f+ t.s sf!  12 + l@ t.t l! ;

: xy, { glyph -- dx dy }
    \ glyph texture_glyph_t-codepoint l@
    x-scale f-scale f* y-scale f-scale f* { f: xs f: ys }
    penxy sf@ penxy sfloat+ sf@ { f: xp f: yp }
    glyph texture_glyph_t-offset_x sl@ xs fm*
    glyph texture_glyph_t-offset_y sl@ ys fm* { f: xo f: yo }
    glyph texture_glyph_t-width  2@ xs fm* ys fm* { f: w f: h }
    xp xo f+ fround 1/2 f-  yp yo f- fround 1/2 f- { f: x0 f: y0 }
    x0 w f+                 y0 h f+                { f: x1 f: y1 }
    glyph texture_glyph_t-s0
    \ over hex. dup $10 dump
    >v
    x0 y0 >xy n> xy-color i>c dup s0t0>st v+
    x1 y0 >xy n> xy-color i>c dup s1t0>st v+
    x0 y1 >xy n> xy-color i>c dup s0t1>st v+
    x1 y1 >xy n> xy-color i>c     s1t1>st v+
    v>
    glyph texture_glyph_t-advance_x sf@ xs f*
    glyph texture_glyph_t-advance_y sf@ ys f* ;

[IFUNDEF] sf+!
    : sf+! ( f addr -- )
	dup sf@ f+ sf! ;
[THEN]

: xy+ ( x y -- )
    penxy sfloat+ sf+!  penxy sf+! ;

: glyph, ( glyph -- dx dy )
    i>off  xy, 2 quad ;
: glyph+xy ( glyph -- )
    glyph, xy+ ;

: all-glyphs ( -- ) 0e atlas# s>f { f: l# f: r# }
    i>off >v
    l# l# >xy n> color @ i>c 0e 0e >st v+
    r# l# >xy n> color @ i>c 1e 0e >st v+
    l# r# >xy n> color @ i>c 0e 1e >st v+
    r# r# >xy n> color @ i>c 1e 1e >st v+
    v> 2 quad ;

0 Value font
Variable last-font#

Defer font#-load ( font# -- font )
Defer font-select# ( xcaddr -- xcaddr num )
' false is font-select#
' noop is font#-load

: font-select ( xc-addr -- xc-addr font )
    font-select# dup last-font# ! font#-load ;

: font->t.i0 ( font -- )
    -2e to t.i0  color f@ to xy-color
    dup texture_font_t-scale sf@ to f-scale
    texture_font_t-atlas @ texture_atlas_t-depth @ 4 = IF
	2e +to xy-color -1e to t.i0  THEN ;

: double-atlas ( font -- )
    dup texture_font_t-atlas @ texture_atlas_t-depth @ 4 = IF
	atlas-bgra# 2* dup >r to atlas-bgra#
    ELSE
	atlas# 2* dup >r to atlas#
    THEN
	r> dup texture_font_enlarge_texture
    atlas-scaletex atlas-bgra-scaletex ;

: glyph@ ( font xc-addr -- font xc-addr glyph )
    BEGIN  2dup texture_font_get_glyph dup 0=  WHILE
	    freetype_gl_errno FTGL_ERR_BASE =
	WHILE  drop over double-atlas  REPEAT  THEN  ?ftgl-ior ;

: glyph-gi@ ( font glyph-index -- font glyph-index glyph )
    BEGIN  2dup texture_font_get_glyph_gi dup 0=  WHILE
	    freetype_gl_errno FTGL_ERR_BASE =
	WHILE  drop over double-atlas  REPEAT  THEN  ?ftgl-ior ;

: xchar+xy (  xc-addrp xc-addr font -- )
    dup font->t.i0
    over glyph@ >r 2drop swap
    dup IF
	r@ swap texture_glyph_get_kerning f-scale f*
	penxy sf@ f+ penxy sf!
    ELSE  drop  THEN
    r> glyph+xy 0e to t.i0 ;

: ?mod-atlas ( -- )
    atlas texture_atlas_t-modified c@ IF
	gen-atlas-tex time( ." atlas: " .!time cr )
	0 atlas texture_atlas_t-modified c!
    THEN ;
: ?mod-atlas-bgra ( -- )
    atlas-bgra texture_atlas_t-modified c@ IF
	gen-atlas-tex-bgra time( ." atlas-bgra: " .!time cr )
	0 atlas-bgra texture_atlas_t-modified c!
    THEN ;

: render> ( -- )
    ?mod-atlas ?mod-atlas-bgra GL_TRIANGLES draw-elements vi0 ;

: ?flush-tris ( n -- ) >r
    i? r@ + points# 2* u>=
    v? r> + points# u>= or
    IF  render>  THEN ;

: ?soft-hyphen { I' I -- xaddr xs }
    I I' over - 2dup x-size { xs }
    "\u00AD" string-prefix?
    IF  I xs + I' =
	IF  "-" drop  ELSE  I xchar+ dup I' over - x-size +to xs  THEN
    ELSE  I  THEN  xs ;

: ?emoji-variant { I' I -- xaddr xs t / xaddr f }
    I I' over - 2dup x-size { xs }  over swap
    xs /string "\uFE0F" string-prefix?
    dup IF  [ "\uFE0F" nip ]L xs + swap  THEN ;

2 Value emoji-font#

: ?font-select { I' I | xs -- xaddr font xs }
    I' I ?emoji-variant IF  to xs  emoji-font#  ELSE
	drop I' I ?soft-hyphen to xs  font-select#  THEN
    dup last-font# ! font#-load  xs ;

-1 value bl/null?

Variable $splits[]

: stacktop ( stack -- addr )
    $@ + cell- ;

: lang-split-string ( addr u -- )
    -1 to bl/null?  last-font# off
    $splits[] $[]free
    bounds ?DO
	{ | xs }
	I' I ?emoji-variant IF  to xs  emoji-font#  ELSE
	    drop I' I ?soft-hyphen to xs  font-select#  THEN
	last-font# @ over last-font# ! <> $splits[] stack# 0= or  IF
	    last-font# @ font#-load { w^ font^ }
	    font^ cell $make $splits[] >stack
	THEN
	xs $splits[] stacktop $+!
    xs +LOOP ;

also harfbuzz
Variable infos[]
Variable positions[]

hb_buffer_create Value hb-buffer
hb-buffer hb_language_get_default hb_buffer_set_language

0 Value numfeatures
#10 Constant maxfeatures
Create userfeatures maxfeatures hb_feature_t * allot
DOES> swap hb_feature_t * + ;

: hb-tag ( addr u -- tag )
    4 <> abort" hb-tags are 4 characters each" be-ul@ ;
: hb-feature! ( feature value addr -- ) >r
    r@ hb_feature_t-tag l!
    r@ hb_feature_t-value l!
    0  r@ hb_feature_t-start l!
    -1 r> hb_feature_t-end l! ;

"dlig" hb-tag 1 0 userfeatures hb-feature!
"liga" hb-tag 1 1 userfeatures hb-feature!
2 to numfeatures

: shape-splits ( -- )
    $splits[] stack# 0 ?DO
	hb-buffer I $splits[] $[]@ over @ >r cell /string
	r@ texture_font_activate_size ?ftgl-ior drop
	0 over hb_buffer_add_utf8
	hb-buffer hb_buffer_guess_segment_properties
	r> texture_font_t-hb_font @ hb-buffer
	0 userfeatures numfeatures hb_shape
	{ | w^ glyph-count }
	hb-buffer glyph-count hb_buffer_get_glyph_infos
	glyph-count l@ hb_glyph_info_t * I infos[] $[]!
	hb-buffer glyph-count hb_buffer_get_glyph_positions
	glyph-count l@ hb_glyph_position_t * I positions[] $[]!
	hb-buffer hb_buffer_reset
    LOOP ;

64e 64e f* 1/f FConstant pos*
64e 1/f FConstant pos*icon

Defer render-string
Defer layout-string
Defer pos-string

: render-shape-string ( addr u -- )
    lang-split-string shape-splits
    $splits[] stack# 0 ?DO
	I $splits[] $[]@ drop @ { font }
	font font->t.i0
	t.i0 -2e f= IF  pos*  ELSE  pos*icon  THEN
	f-scale f* x-scale f*  { f: pos* }
	I positions[] $[]@ drop
	I infos[] $[]@ { pos infos len }
	len 0 ?DO
	    6 ?flush-tris
	    pos I + hb_glyph_position_t-x_offset sl@ pos* fm*
	    pos I + hb_glyph_position_t-y_offset sl@ pos* fm* { f: xo f: yo }
	    xo yo xy+
	    font infos I + hb_glyph_info_t-codepoint l@ glyph-gi@
	    nip nip  glyph,  fdrop fdrop
	    pos I + hb_glyph_position_t-x_advance sl@ pos* fm* xo f-
	    pos I + hb_glyph_position_t-y_advance sl@ pos* fm* yo f- xy+
	hb_glyph_info_t +LOOP
    LOOP ;
previous

: render-simple-string ( addr u -- )
    -1 to bl/null?
    0 -rot  bounds ?DO
	6 ?flush-tris
	I' I ?font-select { xs } xchar+xy
    xs +LOOP  drop ;

: render-us-string ( addr u mask -- )
    penxy sf@ fround 1/2 f+ { f: x0 mask }
    render-string  #12 ?flush-tris
    penxy dup sf@ fround 1/2 f+
    sfloat+ sf@ fround 1/2 f+ { f: x1 f: y }
    s" g" drop font-select { ft } drop
    ft font->t.i0
    ft "–" drop glyph@ { g- } 2drop
    ft "g" drop glyph@ { gg } 2drop
    y
    gg texture_glyph_t-height   sl@
    gg texture_glyph_t-offset_y sl@ - 20% fm*
    f+ fround 1/2 f- { f: y0 }
    g- texture_glyph_t-height @ s>f { f: y1 }
    8 1 DO
	mask I and IF
	    g- texture_glyph_t-s0
	    i>off  >v
	    x0 y0       >xy n> xy-color i>c dup s0t0>st- v+
	    x1 y0       >xy n> xy-color i>c dup s1t0>st- v+
	    x0 y0 y1 f+ >xy n> xy-color i>c dup s0t1>st- v+
	    x1 y0 y1 f+ >xy n> xy-color i>c     s1t1>st- v+
	    v> 2 quad
	THEN
	case I  y
	    1 of
		gg texture_glyph_t-height   sl@
		gg texture_glyph_t-offset_y sl@ - -80% fm*
	    endof
	    2 of
		g- texture_glyph_t-offset_y sl@ s>f
	    endof
	    0e
	endcase  f- fround 1/2 f- to y0
    I +LOOP  0e to t.i0 ;

: xchar@xy ( fw fd fh xc-addrp xc-addr font -- xc-addr fw' fd' fh' )
    { f: fd f: fh }
    dup texture_font_t-scale sf@ { f: f-scale }
    over glyph@ >r 2drop swap
    dup IF
	r@ swap texture_glyph_get_kerning f-scale f* f+
    ELSE  drop  THEN
    r@ texture_glyph_t-advance_x sf@ f-scale f* f+
    r@ texture_glyph_t-offset_y sl@ f-scale fm*
    r> texture_glyph_t-height @ f-scale fm*
    fover f- fd fmax fswap fh fmax ;

: layout-simple-string ( addr u -- fw fd fh ) \ depth is how far it goes down
    0 -rot  0e 0e 0e  bounds ?DO
	I' I ?font-select { xs } xchar@xy
    xs +LOOP  drop ;
also harfbuzz

0e FValue last-pos+
: layout-shape-string ( addr u -- fw fd fh ) \ depth is how far it goes down
    lang-split-string shape-splits  0e to last-pos+
    { | f: fw f: fd f: fh }
    $splits[] stack# 0 ?DO
	I $splits[] $[]@ drop @ { font }
	font font->t.i0
	t.i0 -2e f= IF  pos*  ELSE  pos*icon  THEN f-scale f*  { f: pos* }
	I positions[] $[]@ drop
	I infos[] $[]@ { pos infos len }
	len 0 ?DO
	    pos I + hb_glyph_position_t-x_offset sl@ pos* fm*
	    pos I + hb_glyph_position_t-y_offset sl@ pos* fm* { f: xo f: yo }
	    xo yo xy+
	    font infos I + hb_glyph_info_t-codepoint l@ glyph-gi@ >r 2drop
	    r@ texture_glyph_t-offset_y sl@ f-scale fm*
	    r> texture_glyph_t-height @ f-scale fm*
	    fover f- fd fmax to fd fh fmax to fh
	    pos I + hb_glyph_position_t-x_advance sl@ pos* fm*
	    fdup to last-pos+ +to fw
	hb_glyph_info_t +LOOP
    LOOP
    fw fd fh ;

: pos-simple-string ( fx addr u -- curpos )
    fdup f0< IF  2drop fdrop 0  EXIT  THEN
    dup >r over >r
    0 -rot 0e bounds ?DO
	fdup 0e 0e  I' I ?font-select { xs } xchar@xy
	fdrop fdrop
	{ f: p f: n }
	fdup p f>= fdup n f< and IF
	    I p f- n p f- f2/ f> IF  xchar+  THEN
	    unloop r> - nip  rdrop  EXIT
	THEN  n
    xs +LOOP
    drop rdrop r> fdrop fdrop ;
: pos-shape-string ( addr u fx -- curpos ) \ depth is how far it goes down
    lang-split-string shape-splits { | offset }
    $splits[] stack# 0 ?DO
	I $splits[] $[]@ drop @ { font }
	font font->t.i0
	t.i0 -2e f= IF  pos*  ELSE  pos*icon  THEN
	f-scale f* x-scale f* { f: pos* }
	I positions[] $[]@ drop
	I infos[] $[]@ { pos infos len }
	len 0 ?DO
	    pos I + hb_glyph_position_t-x_advance l@ pos* fm*
	    fover fover f2/ f< IF
		infos I + hb_glyph_info_t-cluster l@ offset +
		fdrop fdrop  unloop unloop  EXIT
	    THEN  f-
	hb_glyph_info_t +LOOP
	I $splits[] $[]@ cell /string +to offset drop
    LOOP
    fdrop offset ;
previous
	
: use-shaper
    ['] render-shape-string is render-string
    ['] layout-shape-string is layout-string
    ['] pos-shape-string is pos-string ;
: use-simple ( -- )
    ['] render-simple-string is render-string
    ['] layout-simple-string is layout-string
    ['] pos-simple-string is pos-string ;

use-shaper

: load-glyph$ ( addr u -- )  layout-string fdrop fdrop fdrop ;
\    bounds ?DO  I font-select nip
\	I texture_font_get_glyph
\	0=  IF  freetype_gl_errno FTGL_ERR_BASE = IF  I double-atlas drop 0
\	    ELSE  0 ?ftgl-ior  THEN
\	ELSE  I I' over - x-size  THEN
\    +LOOP ;

: load-ascii ( -- )
    "#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" load-glyph$ ;

program init

: <render ( -- )
    program glUseProgram
    z-bias set-color+
    .01e 100e 100e >ap
    GL_TEXTURE3 glActiveTexture
    atlas-tex atlas-scaletex
    GL_TEXTURE0 glActiveTexture
    vi0 ;

 : render-bgra> ( -- )
     GL_ONE GL_ONE_MINUS_SRC_ALPHA glBlendFunc
     GL_TRIANGLES draw-elements
     GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA glBlendFunc ;

previous previous
