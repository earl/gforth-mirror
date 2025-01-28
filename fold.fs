\ Constant folding for some primitives

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2019,2020,2024 Free Software Foundation, Inc.

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

3 to: action-of ( interpretation "name" -- xt; compilation "name" -- ; run-time -- xt ) \ core-ext
\G @i{Xt} is the XT that is currently assigned to @i{name}.

: pow2? ( u -- f ) \ gforth pow-two-query
    \g @i{f} is true iff @i{u} is a power of two, i.e., there is
    \g exactly one bit set in @i{u}.
    dup dup 1- and 0= and 0<> ;

: 2lits> ( -- d )  lits> lits> swap ;
: >2lits ( d -- )  swap >lits >lits ;
: 3lits> ( -- t )  2lits> lits> -rot ;
: >3lits ( -- t )  rot >lits >2lits ;
: 4lits> ( -- q )  2lits> 2lits> 2swap ;
: >4lits ( q -- )  2swap >2lits >2lits ;

: cfaprim? ( cfa -- flag )
    [ ' noop >code-address ] Literal
    [ ' image-header >link @ >code-address ] Literal
    \ please do not fold this 1+ into the previous literal
    1+ within ;

: noopt-compile, ( xt -- ) \ gforth-experimental
    \G compiles @var{xt} using the (unoptimized) default method.
    case dup >code-address
	dup cfaprim? ?of  drop  peephole-compile, endof
	docol:      of  :,              endof
	dodoes:     of  does,           endof
	docon:      of  constant,       endof
	dovar:      of  variable,       endof
	douser:     of  user,           endof
	dodefer:    of  defer,          endof
	doabicode:  of  abi-code,       endof
	do;abicode: of  ;abi-code,      endof
	over        ?of peephole-compile, endof \ code word
	lit, lits, postpone execute 0
    endcase ;

0 Value lastfold
: set-fold# ( xt i -- ) 1+ cells lastfold + ! ;
: set-foldmax ( -- addr )
    lastfold @ set-fold# ;
: get-foldmax ( opt-xt -- xt )
    dup @ 1+ cells + @ ;

: foldn: ( xt n -- )
    create  latestxt to lastfold  dup , 1+ 0 ?DO dup , LOOP drop
  DOES> >r lits# r@ @ umin 1+ cells r> + perform ;
: folding ( n -- )
    latest >namehm @ >hmcompile, @ swap
    next-section noname foldn: previous-section
    lastfold set-optimizer ;

: fold-constant: ( pop unpop push n "name" -- )
    ['] noopt-compile, swap foldn:
    noname Create latestxt set-foldmax , , ,
  DOES> ( xt -- ) >r >r
    i' cell+ cell+ perform r> catch-nobt 0= IF
	r> perform EXIT  THEN
    r> cell+ perform ;

: folds ( folder-xt "name1" ... "namen" <eol> -- )
    {: folder-xt :} BEGIN
	>in @ >r parse-name r> >in !
	nip  WHILE
	    folder-xt optimizes
    REPEAT ;

' lits> ' >lits ' noop 1 fold-constant: fold1-0
' fold1-0 folds drop

' lits> ' >lits ' >lits 1 fold-constant: fold1-1
' fold1-1 folds invert abs negate >pow2
' fold1-1 folds 1+ 1- 2* 2/ cells cell/ cell+ cell-
' fold1-1 folds floats sfloats dfloats float+
' fold1-1 folds float/ sfloat/ dfloat/
' fold1-1 folds c>s w>s l>s w>< l>< x><
' fold1-1 folds wcwidth
' fold1-1 folds 0> 0= 0<

' lits> ' >lits ' >2lits 1 fold-constant: fold1-2
' fold1-2 folds dup s>d

' 2lits> ' >lits ' noop 2 fold-constant: fold2-0
' fold2-0 folds 2drop

' 2lits> ' >2lits ' >lits 2 fold-constant: fold2-1
' fold2-1 folds * and or xor
' fold2-1 folds min max umin umax
' fold2-1 folds nip
' fold2-1 folds rshift lshift arshift rol ror
' fold2-1 folds = > >= < <= u> u>= u< u<=
' fold2-1 folds d0> d0< d0=
' fold2-1 folds /s mods

' 2lits> ' >2lits ' >2lits 2 fold-constant: fold2-2
' fold2-2 folds m* um* swap d2* /modf /mods u/mod bounds

' 2lits> ' >2lits ' >3lits 2 fold-constant: fold2-3
' fold2-3 folds over tuck

' 3lits> ' >3lits ' >lits 3 fold-constant: fold3-1
' fold3-1 folds within select mux */f */s u*/

' 3lits> ' >3lits ' >2lits 3 fold-constant: fold3-2
' fold3-2 folds um/mod fm/mod sm/rem du/mod */modf */mods u*/mod under+

' 3lits> ' >3lits ' >3lits 3 fold-constant: fold3-3
' fold3-3 folds rot -rot

' 4lits> ' >4lits ' >lits 4 fold-constant: fold4-1
' fold4-1 folds d= d> d>= d< d<= du> du>= du< du<=

' 4lits> ' >4lits ' >2lits 4 fold-constant: fold4-2
' fold4-2 folds d+ d- 2nip

' 4lits> ' >4lits ' >4lits 4 fold-constant: fold4-4
' fold4-4 folds 2swap

\ optimize +loop (not quite folding)
: replace-(+loop) ( xt1 -- xt2 )
    case
	['] (+loop)       of ['] (/loop) endof
	['] (+loop)-lp+!# of ['] (/loop)-lp+!# endof
	-21 throw
    endcase ;

: (+loop)-optimizer ( xt -- )
    lits# 1 u>= if
	lits> dup >lits 0> if
	    replace-(+loop) then
    then
    peephole-compile, ;

' (+loop)-optimizer optimizes (+loop)
' (+loop)-optimizer optimizes (+loop)-lp+!#

\ optimize pick and fpick

:noname ( xt -- )
    lits# 1 u>= if
	lits> case
	    0 of postpone dup  drop exit endof
	    1 of postpone over drop exit endof
	    [defined] fourth [if]
		2 of postpone third drop exit endof
		3 of postpone fourth drop exit endof
	    [then]
	    dup >lits
	endcase
    then
    peephole-compile, ;
optimizes pick

:noname ( xt -- )
    lits# 1 u>= if
	lits> case
	    0 of postpone fdup  drop exit endof
	    1 of postpone fover drop exit endof
	    [defined] ffourth [if]
		2 of postpone fthird drop exit endof
		3 of postpone ffourth drop exit endof
	    [then]
	    dup >lits
	endcase
    then
    peephole-compile, ;
optimizes fpick

\ optimize + -

: opt+- {: xt: op -- :}
    lits# 1 = if
        0 lits> op ?dup-if
            ['] lit+ peephole-compile, , then
	exit then
    action-of op fold2-1 ;
' opt+- folds + -

: opt* ( xt -- )
    drop lits# 1 = if
        lits> case
            0    of postpone drop 0 lit, endof
	    2    of postpone 2*    endof
	    [ cell 1 sfloats <> ] [IF]
	    1 sfloats of postpone sfloats  endof [THEN]
            cell of postpone cells endof
	    [ cell 1 dfloats <> ] [IF]
	    1 dfloats of postpone dfloats  endof [THEN]
            dup pow2? ?of log2 lit, postpone lshift endof
            dup lit, ['] * peephole-compile,
        endcase
    else
        ['] * fold2-1
    then ;
' opt* optimizes *

\ optimize lit @ into lit@
: opt@ ( xt -- )
    drop lits# 1 u>= if
	lits> ['] lit@ peephole-compile, ,
    else
	['] @ peephole-compile, then ;
' opt@ optimizes @

\ optimize lit execute into call
:noname ( xt -- )
    lits# 1 u>= if
	drop lits> compile,
    else  peephole-compile, then ;
optimizes execute
