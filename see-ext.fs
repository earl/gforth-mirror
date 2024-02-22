\ see-ext.fs extentions for see locals, floats

\ Authors: Anton Ertl, Bernd Paysan
\ Copyright (C) 1995,1996,1997,2003,2007,2012,2014,2019,2021 Free Software Foundation, Inc.

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

\ made extra 26jan97jaw

get-current also see-voc definitions

: c-loop-lp+!#  c-loop cell+ ;
: c-?branch-lp+!#  c-?branch cell+ ;
: c-branch-lp+!#   c-branch  cell+ ;

: c-@local#
    Display? IF
	S" @local" ['] pri-color .string
	dup @ dup cell/ abs 0 <# #S rot sign #> ['] default-color .string bl cemit
    THEN
    cell+ ;

: c-flit
    Display? IF
	dup f@ scratch represent 0=
	IF    2drop  scratch 3 min ['] default-color .string
	ELSE
	    IF  '- cemit  THEN  1-
	    scratch over c@ cemit '. cemit 1 /string ['] default-color .string
	    'E cemit
	    dup abs 0 <# #S rot sign #> ['] default-color .string bl cemit
	THEN THEN
    float+ ;

: c-f@local#
    Display? IF
	S" f@local" ['] pri-color .string
	dup @ dup float/ abs 0 <# #S rot sign #> ['] default-color .string bl cemit
    THEN
    cell+ ;

: c-laddr#
    Display? IF
	S" laddr# " ['] pri-color .string
	dup @ dup abs 0 <# #S rot sign #> ['] default-color .string bl cemit
    THEN
    cell+ ;

: c-lp+!#
    Display? IF
	S" lp+!# " ['] pri-color .string
	dup @ dup abs 0 <# #S rot sign #> ['] default-color .string bl cemit
    THEN
    cell+ ;

create c-extend1
	' @local# A,        ' c-@local# A,
[ifdef] flit ' flit A,      ' c-flit A, [then]
	' f@local# A,       ' c-f@local# A,
	' laddr# A,         ' c-laddr# A,
	' lp+!# A,          ' c-lp+!# A,
        ' ?branch-lp+!# A,  ' c-?branch-lp+!# A,
        ' branch-lp+!# A,   ' c-branch-lp+!# A,
        ' (loop)-lp+!# A,   ' c-loop-lp+!# A,
        ' (+loop)-lp+!# A,  ' c-loop-lp+!# A,
        ' (s+loop)-lp+!# A, ' c-loop-lp+!# A,
        ' (-loop)-lp+!# A,  ' c-loop-lp+!# A,
[IFDEF] (/loop) ' (/loop)-lp+!# A, ' c-loop-lp+!# A, [THEN]
        ' (next)-lp+!# A,   ' c-loop-lp+!# A,
	0 ,		here 0 ,

\ extend see-table
c-extend1 c-extender @ a!
c-extender !

set-current previous
