\ Hashed dictionaries                                  15jul94py

\ Authors: Bernd Paysan, Anton Ertl, Jens Wilke
\ Copyright (C) 1995,1998,2000,2003,2006,2007,2009,2013,2017,2019,2020,2021,2022,2023,2024 Free Software Foundation, Inc.

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

[IFUNDEF] allocate
: reserve-mem here swap allot ;
\ move to a kernel/memory.fs
[ELSE]
: reserve-mem allocate throw ;
[THEN]

[IFUNDEF] hashbits
11 Value hashbits
[THEN]
1 hashbits lshift Value Hashlen

: erase ( addr u -- ) \ core-ext
    \G Clear all bits in @i{u} aus starting at @i{addr}.
    \ !! dependence on "1 chars 1 ="
    ( 0 1 chars um/mod nip )  0 fill ;

\ compute hash key                                     15jul94py

has? ec [IF] [IFUNDEF] hash
: hash ( addr len -- key )
  over c@ swap 1- IF swap char+ c@ + ELSE nip THEN
  [ Hashlen 1- ] literal and ;
[THEN] [THEN]

[IFUNDEF] hash
    [IFDEF] (hashkey2)
	: hash ( addr len -- key )
	    hashbits (hashkey2) ;
    [ELSE]
	: hash ( addr len -- key )
	    hashbits (hashkey1) ;
    [THEN]
[THEN]

Variable insRule        insRule on
Variable revealed

\ Memory handling                                      10oct94py

AVariable HashPointer
Variable HashIndex     \ Number of wordlists
Variable HashPop       \ Number of words
0 AValue HashTable

\ forward declarations
0 AValue hashsearch-map
Defer hash-alloc ( addr -- addr )

\ DelFix and NewFix are from bigFORTH                  15jul94py

: DelFix ( addr root -- ) dup @ third ! ! ;
: NewFix  ( root len # -- addr )
    BEGIN  third @  0= WHILE 2dup * reserve-mem
	    over 0 ?DO  dup 4 pick DelFix third +  LOOP  drop
    REPEAT third @ >r drop r@ @ rot ! r@ swap erase r> ;

: bucket ( addr len wordlist -- bucket-addr )
    \ @var{bucket-addr} is the address of a cell that points to the first
    \ element in the list of the bucket for the string @var{addr len}
    wordlist-extend @ -rot hash xor ( bucket# )
    cells HashTable + ;

: hash-find ( addr len wordlist -- nfa / false )
    >r 2dup r> bucket @ (hashlfind) ;
: hash-rec ( addr len wordlist-id -- nfa translate-nt / 0 )
    ( 0 wordlist-id - ) \ this cancels out, optimizer is not available yet
    hash-find nt>rec ;

\ hash vocabularies                                    16jul94py

: lastlink! ( addr link -- )
  BEGIN  dup @ dup  WHILE  nip  REPEAT  drop ! ;

: (reveal ( nfa wid -- )
    over name>string rot bucket >r
    HashPointer 2 Cells $400 NewFix
    tuck cell+ ! r> insRule @
    IF
	dup @ third ! !
    ELSE
	lastlink!
    THEN
    revealed on 1 HashPop +! 0 hash-alloc drop ;

: hash-reveal ( nfa wid -- )
    2dup (reveal) (reveal ;
: table-reveal ( nfa wid -- )
    2dup (nocheck-reveal) (reveal ;

Create hashvoc-table ' hash-reveal , ' drop , ' n/a , ' hash-reveal , ' n/a ,
Create tablevoc-table ' table-reveal , ' drop , ' n/a , ' table-reveal , ' n/a ,

' [noop] hashvoc-table to-class: hashvoc-to
' [noop] tablevoc-table to-class: tablevoc-to

[IFUNDEF] >link ' noop Alias >link [THEN]

: inithash ( wid -- )
    wordlist-extend
    insRule @ >r  insRule off  1 hash-alloc over ! 0 wordlist-extend -
    dup wordlist-id 0 >link -
    BEGIN  >link @ dup  WHILE  2dup swap (reveal  REPEAT
    2drop  r> insRule ! ;

: addall  ( -- )
    HashPop off voclink
    BEGIN  @ dup WHILE
	    dup 0 wordlist-link -
	    dup wordlist-map @ reveal-method @ >r
	    r@ ['] hashvoc-to = r> ['] tablevoc-to = or
	    IF  inithash ELSE drop THEN
    REPEAT  drop ;

: clearhash  ( -- )
    HashTable Hashlen cells bounds
    DO  I @
	BEGIN  dup  WHILE
	    dup @ swap HashPointer DelFix
	REPEAT
	I !
	cell +LOOP
    HashIndex off 
    voclink
    BEGIN ( wordlist-link-addr )
	@ dup
    WHILE ( wordlist-link )
	dup 0 wordlist-link - ( wordlist-link wid ) 
	dup wordlist-map @ hashsearch-map = 
	IF ( wordlist-link wid )
	    0 swap wordlist-extend !
	ELSE
	    drop
	THEN
    REPEAT
    drop ;

: rehashall  ( wid -- ) 
  drop revealed @ 
  IF 	clearhash addall revealed off 
  THEN ;

: (rehash)   ( wid -- )
  dup wordlist-extend @ 0=
  IF   inithash
  ELSE rehashall THEN ;

' (rehash) hashvoc-table cell+ !
' (rehash) tablevoc-table cell+ !

: hashdouble ( -- )
    HashTable >r clearhash
    1 hashbits 1+ dup  to hashbits  lshift  to hashlen
    r> free >r  0 to HashTable
    addall r> throw ;

\ Create a wordlist by example

forth-wordlist noname-from
' hashvoc-to set-to
' hash-rec set-does>
hm, latestnt >namehm @ to hashsearch-map

\ hash allocate and vocabulary initialization          10oct94py

:noname ( n+ -- n )
  HashTable 0= 
  IF  Hashlen cells reserve-mem TO HashTable
      HashTable Hashlen cells erase THEN
  HashIndex @ swap HashIndex +!
  HashIndex @ Hashlen >=
  [ [IFUNDEF] allocate ]
  ABORT" no more space in hashtable"
  [ [ELSE] ]
  HashPop @ hashlen 2* >= or
  IF  hashdouble  THEN 
  [ [THEN] ] ; is hash-alloc

\ Hash-Find                                            01jan93py
has? cross 0= 
[IF]
: hash-wordlist ( wid -- )
  hashsearch-map swap wordlist-map ! ;
: make-hash
  forth-wordlist hash-wordlist
  environment-wordlist hash-wordlist
  ['] Root >wordlist hash-wordlist
  addall ;
  make-hash \ Baumsuche ist installiert.
[ELSE]
  hashsearch-map forth-wordlist wordlist-map !
[THEN]

\ for ec version display that vocabulary goes hashed

: hash-cold  ( -- )
[ has? ec [IF] ] ." Hashing..." [ [THEN] ]
  HashPointer off  0 TO HashTable  HashIndex off
  addall
\  voclink
\  BEGIN  @ dup WHILE
\         dup 0 wordlist-link - initvoc
\  REPEAT  drop 
[ has? ec [IF] ] ." Done" cr [ [THEN] ] ;

:noname ( -- )
    defers 'cold
    hash-cold
; is 'cold
:noname
    defers 'image
    HashPointer off
    0 to HashTable
; is 'image

: .words  ( -- )
  base @ >r hex HashTable  Hashlen 0
  DO  cr  i 2 .r ." : " dup i cells +
      BEGIN  @ dup  WHILE
             dup cell+ @ name>string type space  REPEAT  drop
  LOOP  drop r> base ! ;

\ \ this stuff is for evaluating the hash function
\ : square dup * ;

\ : countwl  ( -- sum sumsq )
\     \ gives the number of words in the current wordlist
\     \ and the sum of squares for the sublist lengths
\     0 0
\     hashtable Hashlen cells bounds DO
\        0 i BEGIN
\            @ dup WHILE
\            swap 1+ swap
\        REPEAT
\        drop
\        swap over square +
\        >r + r>
\        1 cells
\    +LOOP ;

\ : chisq ( -- n )
\     \ n should have about the same size as Hashlen
\     countwl Hashlen 2 pick */ swap - ;

\ Create hashhist here $100 cells dup allot erase

\ : .hashhist ( -- )  hashhist $100 cells erase
\     HashTable HashLen cells bounds
\     DO  0 I  BEGIN  @ dup  WHILE  swap 1+ swap  REPEAT  drop
\         1 swap cells hashhist + +!
\     cell +LOOP
\     0 0 $100 0 DO
\         hashhist I th@ dup IF
\     	cr I 0 .r ." : " dup .  THEN tuck I * + >r + r>
\     LOOP cr ." Total: " 0 .r ." /" . cr ;
