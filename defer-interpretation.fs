\ Deferred interpretation

\ Authors: Bernd Paysan
\ Copyright (C) 2026 Free Software Foundation, Inc.

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

translate-method: deferring ( ... translation -- ... ) \ gforth-experimental
\G compiling words found in quasi interpretation state into a temporary buffer

obsolete-mask 2/ Constant parsing-mask \ gforth-experimental
\G a parsing word breaks deferred compilation

: parsing ( -- ) \ gforth-experimental
    \G Mark the last word as parsing.
    \G Note that only non-immediate parsing words need this marker
    parsing-mask lastflags or! ;

: parsing-words ( "name1" .. "namex" -- ) \ gforth-experimental
    \G mark existing words as parsing
    BEGIN  parse-name dup WHILE
	    find-name ?dup-IF  make-latest parsing  THEN
    REPEAT 2drop ;

parsing-words char : ' create variable constant value
parsing-words 2variable 2value 2constant fvariable fvalue fconstant
parsing-words varue fvarue 2varue
parsing-words +field begin-structure cfield: wfield: lfield: xfield: field: 2field: ffield: sffield: dffield:
parsing-words value: cvalue: wvalue: lvalue: scvalue: swvalue: slvalue: 2value:
parsing-words fvalue: sfvalue: dfvalue: zvalue: $value: defer: value[]: $value[]:
parsing-words timer: see locate where

$1000 buffer: one-shot-dict
Variable one-shot-dp
one-shot-dict one-shot-dp !

: one-shot-interpret ( xt -- )
    dpp @ >r  one-shot-dp dpp !  catch  r> dpp !  throw ;

: defer-finish ( -- ) \ gforth-experimental
    \G finish the current deferred execution buffer and execute it
    get-state `deferring = IF
	[: ]] ;s ; [[ ;] one-shot-interpret execute
    THEN ;
: defer-start ( -- ) \ gforth-experimental
    \G start new deferred line
    get-state `interpreting = IF
	[: one-shot-dict one-shot-dp ! :noname ;] one-shot-interpret
	`deferring set-state
    THEN ;

: comp2defer ( translator -- )
    dup action-of compiling >r
    :noname r> lit, ]] one-shot-interpret ; [[
    swap is deferring ;

translate-cell        comp2defer
translate-dcell       comp2defer
translate-float       comp2defer
scan-translate-string comp2defer
translate-to          comp2defer
translate-complex     comp2defer
translate-env         comp2defer
:noname ( ... nt -- .. )
    dup >r ?obsolete name>compile one-shot-interpret
    r> >f+c @ parsing-mask and IF  defer-finish defer-start  THEN ;
translate-name is deferring

: deferred-forth ( -- ) \ gforth-experimental
    \G enable deferred interpretation
    ['] defer-start is before-line
    ['] defer-finish is after-line ;

: standard-forth ( -- ) \ gforth-experimental
    ['] noop  dup is before-line is after-line ;
