/* Gforth virtual machine (aka inner interpreter)

  Authors: Anton Ertl, Bernd Paysan, David Kühling
  Copyright (C) 1995,1996,1997,1998,2000,2003,2004,2005,2006,2007,2008,2010,2011,2012,2013,2014,2015,2016,2019,2020,2021,2022 Free Software Foundation, Inc.

  This file is part of Gforth.

  Gforth is free software; you can redistribute it and/or
  modify it under the terms of the GNU General Public License
  as published by the Free Software Foundation, either version 3
  of the License, or (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, see http://www.gnu.org/licenses/.
*/

#if defined(GFORTH_DEBUGGING) || defined(INDIRECT_THREADED) || defined(DOUBLY_INDIRECT) || defined(VM_PROFILING)
#define USE_NO_TOS
#else
#define USE_TOS
#endif

#ifdef __gnu_linux__
#define __USE_MISC 1
#endif

#include "config.h"
#include "forth.h"
#include "symver.h"
#include <ctype.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <assert.h>
#include <stdlib.h>
#include <errno.h>
#include <signal.h>
#include "io.h"
#include "threaded.h"
#ifndef STANDALONE
#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <time.h>
#include <sys/time.h>
#include <unistd.h>
#include <pwd.h>
#include <dirent.h>
#ifdef HAVE_WCHAR_H
#include <wchar.h>
#endif
#include <sys/resource.h>
#ifdef HAVE_FNMATCH_H
#include <fnmatch.h>
#else
#include "fnmatch.h"
#endif
#else
/* #include <systypes.h> */
#endif

#if defined(HAVE_LIBDL) || defined(HAVE_DLOPEN) /* what else? */
#include <dlfcn.h>
#endif
#if defined(_WIN32)
#include <windows.h>
#endif
#ifdef hpux
#include <dl.h>
#endif

#ifdef HAS_FFCALL
#include <avcall.h>
#include <callback.h>
#endif

#ifdef HAS_DEBUG
extern int debug;
# define debugp(x...) do { if (debug) fprintf(x); } while (0)
#endif

#ifndef SEEK_SET
/* should be defined in stdio.h, but some systems don't have it */
#define SEEK_SET 0
#endif

#ifndef HAVE_FSEEKO
#define fseeko fseek
#endif

#ifndef HAVE_FTELLO
#define ftello ftell
#endif

#define NULLC '\0'

#ifdef MEMCMP_AS_SUBROUTINE
extern int gforth_memcmp(const char * s1, const char * s2, size_t n);
extern Char *gforth_memmove(Char * dest, const Char* src, Cell n);
extern Char *gforth_memset(Char * s, Cell c, UCell n);
#define memcmp(s1,s2,n) gforth_memcmp(s1,s2,n)
#define memmove(a,b,c) gforth_memmove(a,b,c)
#define memset(a,b,c) gforth_memset(a,b,c)
#endif

#define NEWLINE	'\n'

/* These two flags control whether divisions are checked by software.
   The CHECK_DIVISION_SW is for those cases where the event is a
   division by zero or overflow on the C level, and might be reported
   by hardware; we might check forr that in autoconf and set the
   switch appropriately, but currently don't.  The CHECK_DIVISION flag
   is for the other cases. */
#ifdef GFORTH_DEBUGGING
#if defined(DIVISION_SIGNAL) && defined(SA_SIGINFO)
/* we know that we get a signal with si_code on division by zero or
   division overflow */
#define CHECK_DIVISION_SW 0
/* we reuse gforth_SP for saving the divisor, as it is not used at the time */
#define SAVE_DIVISOR(x) do { gforth_SP = (Cell *)(x); \
    asm volatile("":"+g"(x)::"memory"); } while (0)
#else /* !(defined(DIVISION_SIGNAL) && defined(SA_SIGINFO)) */
#define CHECK_DIVISION_SW 1
#define SAVE_DIVISOR(x) ((void)0)
#endif /* !(defined(DIVISION_SIGNAL) && defined(SA_SIGINFO)) */
#define CHECK_DIVISION 1
#else
#define CHECK_DIVISION_SW 0
#define CHECK_DIVISION 0
#define SAVE_DIVISOR(x) ((void)0)
#endif

/* buffer for keeping results of stage1 of staged division */
typedef struct {
  UCell inverse;    /* (low part of) the inverse */
  UCell inverse_hi; /* hi part for U/, shift for /F */
  UCell divisor;    /* for computing the modulus */
} stagediv_t;

/* Both the function and the macro work, but on Haswell the function is
   faster because of better register allocation, despite not being inlined */
#if 1
static inline UCell uslashstage2(UCell u1, stagediv_t *stage1)
{
  UDCell hi=ummul(u1,stage1->inverse_hi);
  UDCell lo=ummul(u1,stage1->inverse);
  return DHI(umadd(hi,DHI(lo)));
}
#else
#define uslashstage2(u1,stage1)                                         \
  DHI(umadd(ummul(u1,stage1->inverse_hi), DHI(ummul(u1,stage1->inverse))))
#endif

/* using a function because I did not get macros to work correctly; it
   also happens to have better register allocation on AMD64, at the
   cost of a function call (and non-relocatability) */
static inline Cell slashfstage2(Cell n1, stagediv_t *stage1)
{
  Cell inv=stage1->inverse;
  UDCell ud1=D2UD(mmul(n1,inv));
  UDCell ud2=umadd(ud1,((UCell)inv)>>1);
  Cell n2 = DHI(ud2)+n1;
  return n2>>stage1->inverse_hi;
}

/* these macros did not work */
/*
#define slashfstage2(n1,stage1)                                       \
  (((Cell)(DHI(umadd(D2UD(mmul(n1,stage1->inverse)),stage1->inverse>>1))+n1))>> \
   stage1->inverse_hi)
*/
/*
#define slashfstage2(_n1,_stage1) \
  ({                              \
  Cell n=_n1; \
  stagediv_t *stage1=(stagediv_t *)_stage1; \
  Cell inv=stage1->inverse; \
  UDCell ud1=D2UD(mmul(n,inv)); \
  UDCell ud2=umadd(ud1,((UCell)inv)>>1); \
  Cell n2 = DHI(ud2)+n; \
  n2>>stage1->inverse_hi; \
  })
*/

/* ALIVE_DEBUGGING(x) makes x appear to be used (in the debugging
   engine); we use this in words like DROP to avoid the dead-code
   elimination of the load of the bottom stack item, in order to get
   precise stack underflow errors.

   Here's how it works: We first copy x into a variable _x.  The
   second asm statement makes the compiler believe that it makes use
   of _x.  The first asm statement makes the compiler believe that the
   memory location of x may be clobbered, so the compiler actually has
   to load x into _x if x is a memory reference (e.g., "*lp").
 */
#ifdef GFORTH_DEBUGGING
#define ALIVE_DEBUGGING(x) \
  do { typeof(x) _x=(x);         \
    asm volatile("":::"memory"); \
    asm volatile(""::"X"(_x):"memory");        \
  } while(0)
#else
#define ALIVE_DEBUGGING(x) ((void)0)
#endif

/* KILL is used just before NEXT (ideally just before the goto) to
   make var dead in the preceding code; used for stack-cache registers
   and cfa, to avoid having them appear alive during calls; this mean
   that gcc can use caller-saved registers for them */
#define KILL(var) asm("":"=X"(var))

/* conversion on fetch */

#define vm_Cell2f(_cell,_x)		((_x)=(Bool)(_cell))
#define vm_Cell2c(_cell,_x)		((_x)=(Char)(_cell))
#define vm_Cell2n(_cell,_x)		((_x)=(Cell)(_cell))
#define vm_Cell2w(_cell,_x)		((_x)=(Cell)(_cell))
#define vm_Cell2x(_cell,_x)		((_x)=(Cell)(_cell))
#define vm_Cell2u(_cell,_x)		((_x)=(UCell)(_cell))
#define vm_Cell2a_(_cell,_x)		((_x)=(Cell *)(_cell))
#define vm_Cell2c_(_cell,_x)		((_x)=(Char *)(_cell))
#define vm_Cell2f_(_cell,_x)		((_x)=(Float *)(_cell))
#define vm_Cell2df_(_cell,_x)		((_x)=(DFloat *)(_cell))
#define vm_Cell2sf_(_cell,_x)		((_x)=(SFloat *)(_cell))
#define vm_Cell2xt(_cell,_x)		((_x)=(Xt)(_cell))
#define vm_Cell2f83name(_cell,_x)	((_x)=(struct F83Name *)(_cell))
#define vm_Cell2longname(_cell,_x)	((_x)=(struct Longname *)(_cell))
#define vm_Float2r(_float,_x)		(_x=_float)

/* conversion on store */

#define vm_f2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_c2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_n2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_w2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_u2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_a_2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_c_2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_f_2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_df_2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_sf_2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_xt2Cell(_x,_cell)		((_cell)=(Cell)(_x))
#define vm_f83name2Cell(_x,_cell)	((_cell)=(Cell)(_x))
#define vm_longname2Cell(_x,_cell)	((_cell)=(Cell)(_x))
#define vm_r2Float(_x,_float)		(_float=_x)

#define vm_Cell2Cell(_x,_y)		(_y=_x)

#define IMM_ARG(access,value)		(access)

/* if machine.h has not defined explicit registers, define them as implicit */
#ifndef IPREG
#define IPREG
#endif
#ifndef SPREG
#define SPREG
#endif
#ifndef RPREG
#define RPREG
#endif
#ifndef FPREG
#define FPREG
#endif
#ifndef LPREG
#define LPREG
#endif
#ifndef CAREG
#define CAREG
#endif
#ifndef CFAREG
#define CFAREG
#endif
#ifndef UPREG
#define UPREG
#endif
#ifndef TOSREG
#define TOSREG
#endif
#ifndef spbREG
#define spbREG
#endif
#ifndef spcREG
#define spcREG
#endif
#ifndef spdREG
#define spdREG
#endif
#ifndef speREG
#define speREG
#endif
#ifndef spfREG
#define spfREG
#endif
#ifndef spgREG
#define spgREG
#endif
#ifndef sphREG
#define sphREG
#endif
#ifndef FTOSREG
#define FTOSREG
#endif
#ifndef OPREG
#define OPREG
#endif
#ifndef SPSREG
#define SPSREG
#endif

#ifndef CPU_DEP1
# define CPU_DEP1 0
#endif

/* instructions containing SUPER_END must be the last instruction of a
   super-instruction (e.g., branches, EXECUTE, and other instructions
   ending the basic block). Instructions containing SET_IP get this
   automatically, so you usually don't have to write it.  If you have
   to write it, write it after IP points to the next instruction.
   Used for profiling.  Don't write it in a word containing SET_IP, or
   the following block will be counted twice. */
#ifdef VM_PROFILING
#define SUPER_END  vm_count_block(IP)
#else
#define SUPER_END
#endif
#define SUPER_CONTINUE

#if defined(ASMCOMMENT) && !defined(__clang__)
/* an individualized asm statement so that (hopefully) gcc's optimizer
   does not do cross-jumping */
#define asmcomment(string) asm volatile(ASMCOMMENT string)
#else
/* we don't know how to do an asm comment, so we just do an empty asm */
#define asmcomment(string) asm("")
#endif

#define DEPTHOFF 0
#ifdef GFORTH_DEBUGGING
#if DEBUG
#define NAME(string) if(debug) { saved_ip=ip; asmcomment(string); fprintf(stderr,"%08lx depth=%3ld tos=%016lx: "string"\n",(Cell)ip,((user_area*)up)->sp0+DEPTHOFF-sp,sp[0]);}
#else /* !DEBUG */
#define NAME(string) { saved_ip=ip; asmcomment(string); }
/* the asm here is to avoid reordering of following stuff above the
   assignment; this is an old-style asm (no operands), and therefore
   is treated like "asm volatile ..."; i.e., it prevents most
   reorderings across itself.  We want the assignment above first,
   because the stack loads may already cause a stack underflow. */
#endif /* !DEBUG */
#elif DEBUG
#       define  NAME(string)    if(debug) {Cell __depth=((user_area*)up)->sp0+DEPTHOFF-sp; int i; fprintf(stderr,"%08lx depth=%3ld: "string,(Cell)ip,((user_area*)up)->sp0+DEPTHOFF-sp); for (i=__depth-1; i>0; i--) fprintf(stderr, " $%lx",sp[i]); fprintf(stderr, " $%lx\n",spTOS); }
#elif ASMNAME
#	define	NAME(string) asmcomment(string);
#else
#	define  NAME(string)
#endif
#define	NAME1(string) asmcomment(string);

#ifdef DEBUG
#define CFA_TO_NAME(__cfa) \
  Cell len = LONGNAME_COUNT(__cfa); \
  char * name = LONGNAME_NAME(__cfa);
#endif

#ifdef SIGWINCH
extern Cell winch_addr;
#endif

#ifdef STANDALONE
jmp_buf * throw_jmp_handler;

void throw(int code)
{
  longjmp(*throw_jmp_handler,code); /* !! or use siglongjmp ? */
}
#endif

#if defined(HAS_FFCALL) || defined(HAS_LIBFFI)
#define SAVE_REGS IF_fpTOS(fp[0]=fpTOS); gforth_SP=sp; gforth_FP=fp; gforth_RP=rp; gforth_LP=lp;
#define REST_REGS sp=gforth_SP; fp=gforth_FP; rp=gforth_RP; lp=gforth_LP; IF_fpTOS(fpTOS=fp[0]);
#endif

#if !defined(ENGINE)
/* normal engine */
#define VARIANT(v)	(v)
#define JUMP(target)	goto I_noop
#define LABEL(name) H_##name: asm(ASMCOMMENT "I " #name); \
     I_##name:
#define LABEL_UU(name) H_##name: MAYBE_UNUSED asm(ASMCOMMENT "I " #name); \
     I_##name: MAYBE_UNUSED
#define LABEL3(name) J_##name: { \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  }
#define LABEL3_UU(name) J_##name: MAYBE_UNUSED { \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  }

#elif ENGINE==2
/* variant with padding between VM instructions for finding out
   cross-inst jumps (for dynamic code) */
#define gforth_engine gforth_engine2
#define VARIANT(v)	(v)
#define JUMP(target)	goto I_noop
#define LABEL(name) H_##name: asm(ASMCOMMENT "H " #name); SKIP16; \
    asm(ASMCOMMENT "I " #name); I_##name:
#define LABEL_UU(name) H_##name: MAYBE_UNUSED asm(ASMCOMMENT "H " #name); SKIP16; \
    asm(ASMCOMMENT "I " #name); I_##name: MAYBE_UNUSED
/* the SKIP16 after LABEL3 is there, because the ARM gcc may place
   some constants after the final branch, and may refer to them from
   the code before label3.  Since we don't copy the constants, we have
   to make sure that such code is recognized as non-relocatable. */
#define LABEL3(name) J_##name: { \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  } SKIP16;
#define LABEL3_UU(name) J_##name: MAYBE_UNUSED { \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  } SKIP16;

#elif ENGINE==3
/* variant with different immediate arguments for finding out
   immediate arguments (for native code) */
#define gforth_engine gforth_engine3
#define VARIANT(v)	((v)^0xffffffff)
#define JUMP(target)	goto K_lit
#define LABEL(name) H_##name: asm(ASMCOMMENT "I " #name); \
    I_##name:
#define LABEL_UU(name) H_##name: MAYBE_UNUSED asm(ASMCOMMENT "I " #name); \
    I_##name: MAYBE_UNUSED 
#define LABEL3(name) J_##name: { \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  }
#define LABEL3_UU(name) J_##name: MAYBE_UNUSED { \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);                  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
    asm(ASMCOMMENT "J " #name);  \
  }
#else
#error illegal ENGINE value
#endif /* ENGINE */

/* the asm(""); is there to get a stop compiled on Itanium */
#define LABEL2(name) K_##name: asm(ASMCOMMENT "K " #name);
#define LABEL2_UU(name) K_##name: MAYBE_UNUSED asm(ASMCOMMENT "K " #name);

#define LABEL1(name) L_##name:  MAYBE_UNUSED { \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
    asm(ASMCOMMENT "L " #name);  \
  }

Label *gforth_engine(Xt *ip0 sr_proto)
/* executes code at ip, if ip!=NULL
   returns array of machine code labels (for use in a loader), if ip==NULL
*/
{
  register stackpointers * SPs SPSREG = in_SPs;
#undef gforth_SP
#undef gforth_RP
#undef gforth_LP
#undef gforth_UP
#define gforth_SP (SPs->spx)
#define gforth_RP (SPs->rpx)
#define gforth_LP (SPs->lpx)
#define gforth_UP (SPs->upx)
#if defined(GFORTH_DEBUGGING)
# undef saved_ip
# define rp (SPs->s_rp)
# define saved_ip (SPs->s_ip)
#else /* !defined(GFORTH_DEBUGGING) */
  register Cell *rp RPREG;
# undef saved_ip
  Xt* MAYBE_UNUSED saved_ip;
#endif /* !defined(GFORTH_DEBUGGING) */
  register Xt *ip IPREG = ip0;
  register Cell *sp SPREG = gforth_SP;
  register Float *fp FPREG = gforth_FP;
  register Address lp LPREG = gforth_LP;
  register Xt cfa CFAREG;
  register Label real_ca CAREG;
#ifdef HAS_OBJECTS
  register Char * op OPREG = NULL;
#endif
#ifdef MORE_VARS
  MORE_VARS
#endif
#ifdef HAS_FFCALL
  av_alist alist;
  extern va_alist gforth_clist;
  float frv;
  int irv;
  double drv;
  long long llrv;
  void * prv;
#endif
  register user_area* up UPREG = gforth_UP;
#if !defined(GFORTH_DEBUGGING)
  register Cell MAYBE_UNUSED spTOS TOSREG;
  register Cell MAYBE_UNUSED spb spbREG;
  register Cell MAYBE_UNUSED spc spcREG;
  register Cell MAYBE_UNUSED spd spdREG;
  register Cell MAYBE_UNUSED spe speREG;
  register Cell MAYBE_UNUSED spf spfREG;
  register Cell MAYBE_UNUSED spg spgREG;
  register Cell MAYBE_UNUSED sph sphREG;
  IF_fpTOS(register Float fpTOS FTOSREG;)
#endif /* !defined(GFORTH_DEBUGGING) */
#if defined(DOUBLY_INDIRECT)
  static Label *symbols;
  static void *routines[]= {
#define MAX_SYMBOLS (sizeof(routines)/sizeof(routines[0]))
#else /* !defined(DOUBLY_INDIRECT) */
  static Label symbols[]= {
#define MAX_SYMBOLS (sizeof(symbols)/sizeof(symbols[0]))
#endif /* !defined(DOUBLY_INDIRECT) */
#define INST_ADDR(name) ((Label)&&I_##name)
#include PRIM_LAB_I
#undef INST_ADDR
    (Label)0,
#define INST_ADDR(name) ((Label)&&L_##name)
#include PRIM_LAB_I
#undef INST_ADDR
#define INST_ADDR(name) ((Label)&&K_##name)
#include PRIM_LAB_I
#undef INST_ADDR
#define INST_ADDR(name) ((Label)&&J_##name)
#include PRIM_LAB_I
#undef INST_ADDR
    (Label)&&after_last,
    (Label)&&before_goto,
    (Label)&&after_goto,
/* just mention the H_ labels, so the SKIP16s are not optimized away */
#define INST_ADDR(name) ((Label)&&H_##name)
#include PRIM_LAB_I
#undef INST_ADDR
  };
#ifdef STANDALONE
#define INST_ADDR(name) ((Label)&&I_##name)
#include "image.i"
#undef INST_ADDR
#endif
#ifdef CPU_DEP2
  CPU_DEP2
#endif

  rp = SPs->rpx;
#ifdef DEBUG
  debugp(stderr,"ip=%lx, sp=%lx, rp=%lx, fp=%lx, lp=%lx, up=%lx\n",
	 (Cell)ip0,(Cell)sp,(Cell)rp,
	 (Cell)fp,(Cell)lp,(Cell)up);
#endif

  if (ip0 == NULL) {
#if defined(DOUBLY_INDIRECT)
#define CODE_OFFSET (26*sizeof(Cell))
#define XT_OFFSET (22*sizeof(Cell))
#define LABEL_OFFSET (18*sizeof(Cell))
    int i;
    Cell code_offset = offset_image? CODE_OFFSET : 0;
    Cell xt_offset = offset_image? XT_OFFSET : 0;
    Cell label_offset = offset_image? LABEL_OFFSET : 0;

    debugp(stderr, "offsets code/xt/label: %lx/%lx/%lx\n",
	   code_offset, xt_offset, label_offset);

    symbols = (Label *)(malloc(MAX_SYMBOLS*sizeof(Cell)+CODE_OFFSET)+code_offset);
    xts = (Label *)(malloc(MAX_SYMBOLS*sizeof(Cell)+XT_OFFSET)+xt_offset);
    labels = (Label *)(malloc(MAX_SYMBOLS*sizeof(Cell)+LABEL_OFFSET)+label_offset);
    
    for (i=0; i<DOER_MAX+1; i++) {
      labels[i] = routines[i];
      xts[i] = symbols[i] = (Label)routines[i];
    }
    for (; routines[i]!=0; i++) {
      if (i+1>=MAX_SYMBOLS) {
	fprintf(stderr,"gforth-ditc: more than %ld primitives\n",(long)MAX_SYMBOLS);
	exit(1);
      }
      labels[i] = routines[i];
      xts[i] = symbols[i] = (Label)&labels[i];
    }
    labels[i] = xts[i] = symbols[i] = 0;
#endif /* defined(DOUBLY_INDIRECT) */
#ifdef STANDALONE
    return image;
#else
    return symbols;
#endif
  }

#ifdef USE_TOS
  sp += STACK_CACHE_DEFAULT-1;
  /* some of those registers are dead, but its simpler to initialize them all */  spTOS = sp[0];
  spb = sp[-1];
  spc = sp[-2];
  spd = sp[-3];
  spe = sp[-4];
  spf = sp[-5];
  spg = sp[-6];
  sph = sp[-7];
#endif

  IF_fpTOS(fpTOS = fp[0]);
/*  prep_terminal(); */
  SET_IP(ip);
  SUPER_END; /* count the first block, too */
  NEXT;

#ifdef CPU_DEP3
  CPU_DEP3
#endif

#include PRIM_I
  after_last: 
  /*needed only to get the length of the last primitive */
  FIRST_NEXT;
  LABEL_UU(return_0)
  NEXT; /* gcc-4.9 workaround: Requires yet another dummy NEXT */
  return 0;
}
