\ recognizer-based interpreter                       05oct2011py

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2012,2013,2014,2015,2016,2017,2018,2019,2020,2021,2022,2023,2024 Free Software Foundation, Inc.

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

\ Recognizer are words that take a string and try to figure out
\ what to do with it.  I want to separate the parse action from
\ the interpret/compile/postpone action, so that recognizers
\ are more general than just be used for the interpreter.

\ The "design pattern" used here is the *factory*, even though
\ the recognizer does not return a full-blown object.
\ A recognizer has the stack effect
\ ( addr u -- token table | addr u 0 )
\ where the token is the result of the parsing action (can be more than
\ one stack or live on other stacks, e.g. on the FP stack)
\ and the table contains three actions (as array of three xts):
\ interpret it, compile it, postpone it.

' >lits Alias lit, ( x -- ) \ gforth
\G This is a non-immediate variant of @word{literal}@*
\G Execution semantics: Compile the following semantis:@*
\G Compiled semantics: ( @i{ -- x} ).

: 2lit, ( x1 x2 -- ) postpone 2literal ;
\G This is a non-immediate variant of @word{2literal}@*
\G Execution semantics: Compile the following semantis:@*
\G Compiled semantics: ( @i{ -- x1 x2} ).

: no.extensions  ( -- ) #-13 throw ;

: do-translate ( ... translator -- ... ) \ gforth-internal
    state @ abs cells + @ execute-;s ;
: translate: ( int-xt comp-xt post-xt "name" -- ) \ gforth-experimental
    \G Defines @i{name}, a translator containing @i{int-xt},
    \G @i{comp-xt}, and @i{post-xt}.  In all the following
    \G descriptions @i{data} is the data that the recognizer pushes
    \G below the translator.@*
    \G Executing @i{int-xt} @samp{( @i{... data -- ...} )} performs
    \G the interpretation semantics represented by @i{data xt-name}.@*
    \G Executing @i{comp-xt} @samp{( @i{... data -- ...} )} performs
    \G the compilation semantics represented by @i{data xt-name}.@*
    \G Executing @i{post-xt} @samp{( @i{data -- } )} compiles the
    \G compilation semantics represented by @i{data xt-name}.
    Create swap rot , , , 7 0 DO  ['] no.extensions ,  LOOP
    ['] do-translate set-does> ;

0 Value translate-fallback-error \ set to true to prevent fallback

Create postponing ( translator -- ) \ gforth-experimental
\G perform postpone action of translator
2 cells ,
DOES> @ over >does-code ['] do-translate = IF
      + @ execute-;s  THEN
  \ fallback for combined translators
  translate-fallback-error IF  #-21 throw  THEN
  true warning" translator not defined by translate:"
  cell/ dup state @ abs = IF  drop execute-;s  THEN
  negate state !@ >r execute r> state ! ;

: name-compsem ( ... nt -- ... )
    \ perform compilation semantics of nt
    ?obsolete name>compile execute-;s ;

forth-wordlist is rec-nt
:noname ['] rec-nt >body ; is context

:noname name?int  execute-;s ;
' name-compsem
:noname  lit, postpone name-compsem ;
translate: translate-nt ( ... nt -- ... ) \ gforth-experimental
\G Translate @i{nt}.  The @i{...} are there because the interpretation
\G or compilation semantics of @i{nt} might have a stack effect.

' noop
' lit,
:noname lit, postpone lit, ;
translate: translate-num ( x -- ... ) \ gforth-experimental
\G translate a number

' noop
' 2lit,
:noname 2lit, postpone 2lit, ;
translate: translate-dnum ( dx -- ... ) \ gforth-experimental
\G translate a double number

: ?found ( token|0 -- token ) \ gforth-experimental
    \G @code{throw}s -13 (undefined word) if @var{token} is 0.
    dup 0= #-13 and throw ;
: translate-nt? ( token -- flag )
    \G check if name token; postpone action may differ
    dup IF  >body 2@ ['] translate-nt >body 2@ d=  THEN ;
: nt>rec ( nt / 0 -- nt translate-nt / 0 )
    dup IF  dup where, ['] translate-nt  THEN ;

\ snumber? should be implemented as recognizer stack

: rec-num ( addr u -- n/d table | 0 ) \ gforth-experimental
    \G converts a number to a single/double integer
    snumber?  dup
    IF
	0> IF  ['] translate-dnum  ELSE  ['] translate-num  THEN  EXIT
    THEN ;

\ generic stack get/set; actually, we don't need this for
\ the recognizer any more, but other parts of the kernel use it.

: get-stack ( stack -- x1 .. xn n ) \ gforth-experimental
    \G Push the contents of @i{stack} on the data stack, with the top
    \G element in @i{stack} being pushed as @i{xn}.
    $@ dup cell/ >r bounds ?DO  I @  cell +LOOP  r> ;

: set-stack ( x1 .. xn n stack -- ) \ gforth-experimental
    \G Overwrite the contents of @i{stack} with @i{n} elements from
    \G the data stack, with @i{xn} becoming the top of @i{stack}.
    >r cells r@ $!len
    r> $@ bounds cell- swap cell- U-DO  I !  cell -LOOP ;

: stack: ( n "name" -- ) \ gforth-experimental stack-colon
    \G Create a named stack with at least @var{n} cells space.
    drop $Variable ;
: do-stack: ( x1 .. xn n xt "name" -- )
    >r dup stack: r> set-does> latestxt >body set-stack ;
: stack ( n -- stack ) \ gforth-experimental
    \G Create an unnamed stack with at least @var{n} cells space.
    drop align here 0 , ;

: >stack ( x stack -- ) \ gforth-experimental to-stack
    \G Push @i{x} to top of @i{stack}.
    dup >r $@len cell+ r@ $!len
    r> $@ + cell- ! ;
: stack> ( stack -- x ) \ gforth-experimental stack-from
    \G Pop item @i{x} from top of @i{stack}.
    dup >r $@ ?dup-IF  + cell- @ r@ $@len cell- r> $!len
    ELSE  drop rdrop  THEN ;
: stack# ( stack -- elements )
    $@len cell/ ;

: minimal-recognize ( addr u -- ... translate-xt / 0 ) \ gforth-internal
    \g Sequence of @code{rec-nt} and @code{rec-num}
    2>r 2r@ rec-nt dup 0= IF  drop 2r@ rec-num  THEN  2rdrop ;

( ' rec-num ' rec-nt 2 combined-recognizer: default-recognize ) \ see pass.fs
\G The system recognizer
Defer forth-recognize ( c-addr u -- ... translate-xt ) \ recognizer
\G The system recognizer: @word{forth-recognize} is a @word{defer}red
\G word that contains a recognizer (sequence).  The system's text
\G interpreter calls @word{forth-recognize}.
' minimal-recognize is forth-recognize

: [ ( -- ) \  core	left-bracket
    \G Enter interpretation state. Immediate word.
    state off ; immediate

: ] ( -- ) \ core	right-bracket
    \G Enter compilation state.
    state on  ;

: postpone ( "name" -- ) \ core
    \g Compiles the compilation semantics of @i{name}.
    parse-name forth-recognize ?found postponing
; immediate restrict
