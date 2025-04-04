\ various tests, especially for bugs that have been fixed
\ or where we don't use the testing framework

\ Authors: Anton Ertl, Bernd Paysan
\ Copyright (C) 1997,1998,2000,2003,2007,2013,2015,2019,2023,2024 Free Software Foundation, Inc.

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

\ (hashkey2)

create (hashkey2)-buffer 5000 allot
variable (hashkey2)-broken
: test1-(hashkey2) { u -- }
    \ test keys of length u; max length 32
    (hashkey2)-broken off
    s" abcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=" { sa sl }
    sa 8 + u 8 cells (hashkey2) { key }
    4100 0 do \ at least as many as given in the "pagesize" in hashkey2a()
        sa (hashkey2)-buffer i + tuck sl move ( addr )
        8 + u 8 cells (hashkey2) key <> if
            cr ." wrong hash at " sa i + 8 + h. u h.
            (hashkey2)-broken on then
    loop ;

: test-(hashkey2) ( -- )
    26 0 do i test1-(hashkey2) loop
    (hashkey2)-broken @ abort" (hashkey2) broken" ;

test-(hashkey2)

\ combination of marker and locals
marker foo1
marker foo2
foo2

: bar { xxx yyy } ;

foo1

\ locals in an if structure
: locals-test1
    lp@ swap
    if
	{ a } a
    else
    endif
    lp@ <> abort" locals in if error 1" ;

0 locals-test1
1 locals-test1


\ recurse and locals

: fac { n -- n! }
    n 0>
    if
	n 1- recurse n *
    else
	1
    endif ;

5 fac 120 <> throw

\ TO and locals

: locals-test2 ( -- )
    true dup dup dup { addr1 u1 addr2 u2 -- n }
    false TO addr1
    addr1 false <> abort" TO does not work on locals" ;
locals-test2

: locals-test3 ( -- )
    \ this should compile, but gives "invalid name argument" on gforth-0.3.0
    0 { a b } 0 to a ;

\ multiple reveals (recursive)

0
: xxx recursive ;
throw \ if the TOS is not 0, throw an error

\ look for primitives

\ ' + xt>threaded threaded>name dup 0= throw ( nt )
\ s" +" find-name <> throw

\ represent

1e pad 5 represent -1 <> swap 0 <> or swap 1 <> or throw

\ -trailing

s" a     " 2 /string -trailing throw drop

\ convert (has to skip first char)

#0. s" 123  " drop convert drop #23. d<> throw

\ search

parse-name abc 2dup parse-name xyza search throw d<> throw
parse-name b   2dup parse-name abc  search throw d<> throw

\ only

: test-only ( -- )
    get-order get-current
    0 set-current
    only
    get-current >r
    set-current set-order
    r> abort" ONLY sets current" ;
test-only

\ create-interpret/compile

[IFDEF] interpretation>
-1 warnings !@

: my-constant ( n "name" -- )
    create-interpret/compile
    ,
interpretation>
    @
<interpretation
compilation>
    @ postpone literal
<compilation ;

warnings !

5 my-constant five
five 5 <> throw
: five' five ;
five' 5 <> throw

[THEN]

\ structs and alignment

struct
  char% field field1
  float% field field2
end-struct my-struct%

0 field2 float% %alignment <> throw

\ filenames with "//"

s" //jkfklfggfld/fjsjfk/hjfdjs" open-fpath-file 2drop

\ allotting negative space

1 allot
-1 allot

\ unaligned input for xt?

here 1+ xt? throw

\ [compile] exit = exit

: foo [compile] exit abort" '[compile] exit' broken" ;
foo

\ restore-input

: test-restore-input[ ( -- )
    refill 0= abort" refill failed"
    bl word drop
    save-input
    refill 0= abort" refill failed"
    -1 ;

: ]test-restore-input ( -- )
    drop restore-input abort" restore-input failed" 0 ;

\ First input is skipped until the "]test-restore-input", then it is
\ reset to just before "0 [if]"
test-restore-input[ abort \ these aborts are skipped
abort 0 [if]
    s" oops" 2drop ]test-restore-input abort
[then]
( 0 ) throw

\ the same test with CRLF newlines
test-restore-input[ abort \ these aborts are skipped
abort 0 [if]
    s" oops" 2drop ]test-restore-input abort
[then]
( 0 ) throw

\ comments across several lines

( fjklfjlas;d
abort" ( does not work across lines"
)

s" ( testing ( without being delimited by newline in non-files" evaluate

\ constant folding must not produce a compile-time error
: test 1 0 / ;

\ the following standard program must work with automatic scoping
: foo0 {: xxxxx :} ahead begin xxxxx again then ;
:noname {: xxxxx :} ahead begin xxxxx again then ; drop


\ and the equivalent for quotations and closures
: foo1 [: {: xxxxx :} ahead begin xxxxx again then ;] ;
: foo2 [n:d {: xxxxx :} ahead begin xxxxx again then ;] ;
: foo3 [{: xxxxx :}h1 {: yyyyy :} ahead begin xxxxx yyyyy again then ;


\ last test!
\ testing '(' without ')' at end-of-file
." expect ``" sourcefilename type ':' emit sourceline# 3 + 0 .r ." :1: warning: ')' missing''" cr
(

