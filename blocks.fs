\ A less simple implementation of the blocks wordset. 

\ Authors: Anton Ertl, Bernd Paysan, Neal Crook, Jens Wilke
\ Copyright (C) 1995,1996,1997,1998,2000,2003,2006,2007,2008,2011,2012,2016,2019,2021,2022,2023,2024 Free Software Foundation, Inc.

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


\ A more efficient implementation would use mmap on OSs that
\ provide it and many buffers on OSs that do not provide mmap.

\ Now, the replacement algorithm is "direct mapped"; change to LRU
\ if too slow. Using more buffers helps, too.

\ I think I avoid the assumption 1 char = 1 here, but I have not tested this

\ 1024 constant chars/block \ mandated by the standard

require struct.fs

struct
    cell%		field buffer-block   \ the block number
    cell%		field buffer-fid     \ the block's fid
    cell%		field buffer-dirty   \ the block dirty flag
    char% chars/block * field block-buffer   \ the data
    cell% 0 *		field next-buffer
end-struct buffer-struct

Variable block-buffers
Variable last-block

$20 Value buffers

\ limit block files to 2GB; gforth <0.6.0 erases larger block files on
\ 32-bit systems
$200000 Value block-limit

User block-fid
User block-offset ( -- addr ) \ gforth
\G User variable containing the number of the first block (default
\G since 0.5.0: 0).  Block files created with Gforth versions before
\G 0.5.0 have the offset 1.  If you use these files you can: @code{1
\G offset !}; or add 1 to every block number used; or prepend 1024
\G characters to the file.
0 block-offset !  \ store 1 here fore 0.4.0 compatibility

' block-offset alias offset \ !! eliminate this?

: block-cold ( -- )
    block-fid off  last-block off
    buffer-struct buffers * %alloc dup block-buffers ! ( addr )
    dup buffer-struct %size buffers * erase
    buffers 0 ?DO dup buffer-block on next-buffer LOOP drop ;

:is 'cold ( -- )
    defers 'cold block-cold ;

:noname ( -- ) defers 'image  block-buffers off ; is 'image

block-cold

Defer flush-blocks ( -- ) \ gforth-internal

: open-blocks ( c-addr u -- ) \ gforth
\g Use the file, whose name is given by @i{c-addr u}, as the blocks file.
    try ( c-addr u )
	2dup open-fpath-file throw
	rot close-file throw  2dup file-status throw bin open-file throw
	>r 2drop r>
    endtry-iferror ( c-addr u ior )
	>r 2dup file-status nip 0= r> and throw \ does it really not exist?
	r/w bin create-file throw
    then
    block-fid @ IF
	flush-blocks block-fid @ close-file throw
    THEN
    block-fid ! ;

: use ( "file" -- ) \ gforth
    \g Use @i{file} as the blocks file.
    parse-name name-too-short? open-blocks ;

\ the file is opened as binary file, since it either will contain text
\ without newlines or binary data
: get-block-fid ( -- wfileid ) \ gforth
    \G Return the file-id of the current blocks file. If no blocks
    \G file has been opened, use @file{blocks.fb} as the default
    \G blocks file.
    block-fid @ 0=
    if
	s" blocks.fb" open-blocks
    then
    block-fid @ ;

: block-position ( u -- ) \ block
\G Position the block file to the start of block @i{u}.
    dup block-limit u>= -35 and throw
    offset @ - chars/block chars um* get-block-fid reposition-file throw ;

: update ( -- ) \ block
    \G Mark the state of the current block buffer as assigned-dirty.
    last-block @ ?dup-IF  buffer-dirty on  THEN ;

: save-buffer ( buffer -- ) \ gforth
    dup
    >r buffer-dirty @
    if
	r@ buffer-fid @ 0= IF  get-block-fid r@ buffer-fid !  THEN
	r@ buffer-block @ block-position
	r@ block-buffer chars/block  r@ buffer-fid @  write-file throw
	r@ buffer-fid @ flush-file throw
	r@ buffer-dirty off 
    endif
    rdrop ;

: empty-buffer ( buffer -- ) \ gforth
    dup buffer-block on buffer-dirty off ;

: save-buffers  ( -- ) \ block
    \G Transfer the contents of each @code{update}d block buffer to
    \G mass storage, then mark all block buffers as assigned-clean.
    block-buffers @
    buffers 0 ?DO dup save-buffer next-buffer LOOP drop ;

: empty-buffers ( -- ) \ block-ext
    \G Mark all block buffers as unassigned; if any had been marked as
    \G assigned-dirty (by @code{update}), the changes to those blocks
    \G will be lost.
    block-buffers @
    buffers 0 ?DO dup empty-buffer next-buffer LOOP drop ;

: flush ( -- ) \ block
    \G Perform the functions of @code{save-buffers} then
    \G @code{empty-buffers}.
    save-buffers
    empty-buffers ;

' flush IS flush-blocks

: get-buffer ( u -- a-addr ) \ gforth-internal
    0 buffers um/mod drop buffer-struct %size * block-buffers @ + ;

: block ( u -- a-addr ) \ block
    \G If a block buffer is assigned for block @i{u}, return its
    \G start address, @i{a-addr}. Otherwise, assign a block buffer
    \G for block @i{u} (if the assigned block buffer has been
    \G @code{update}d, transfer the contents to mass storage), read
    \G the block into the block buffer and return its start address,
    \G @i{a-addr}.
    dup offset @ u< -35 and throw
    dup get-buffer >r
    dup r@ buffer-block @ <>
    r@ buffer-fid @ block-fid @ <> or
    if
	r@ save-buffer
	dup block-position
	r@ block-buffer chars/block get-block-fid read-file throw
	\ clear the rest of the buffer if the file is too short
	r@ block-buffer over chars + chars/block rot chars - blank
	r@ buffer-block !
	get-block-fid r@ buffer-fid !
    else
	drop
    then
    r> dup last-block ! block-buffer ;

: buffer ( u -- a-addr ) \ block
    \G If a block buffer is assigned for block @i{u}, return its
    \G start address, @i{a-addr}. Otherwise, assign a block buffer
    \G for block @i{u} (if the assigned block buffer has been
    \G @code{update}d, transfer the contents to mass storage) and
    \G return its start address, @i{a-addr}.  The subtle difference
    \G between @code{buffer} and @code{block} mean that you should
    \G only use @code{buffer} if you don't care about the previous
    \G contents of block @i{u}. In Gforth, this simply calls
    \G @code{block}.
    \ reading in the block is unnecessary, but simpler
    block ;

User scr ( -- a-addr ) \ block-ext s-c-r
    \G @code{User} variable containing
    \G the block number of the block most recently processed by
    \G @code{list}.
0 scr !

\ nac31Mar1999 moved "scr @" to list to make the stack comment correct
: updated?  ( n -- f ) \ gforth
    \G Return true if @code{updated} has been used to mark block @i{n}
    \G as assigned-dirty.
    buffer
    [ 0 buffer-dirty 0 block-buffer - ] Literal + @ ;

: list ( u -- ) \ block-ext
    \G Display block @i{u}. In Gforth, the block is displayed as 16
    \G numbered lines, each of 64 characters.
    \ calling block again and again looks inefficient but is necessary
    \ in a multitasking environment
    dup scr !
    ." Screen " u.
    scr @ updated?  0= IF ." not "  THEN  ." modified     " cr
    l/s 0
    ?do
	i 2 .r space scr @ block i c/l * chars + c/l type cr
    loop ;

:noname  2 <> -12 and throw >in ! blk ! ;
                              \ restore-input
:noname  blk @ >in @ 2 ;      \ save-input
:noname  2 ;                  \ source-id "*a block*"
:noname  1 blk +! 1 loadline +! >in off true ;      \ refill
:noname  blk @ block chars/block ;  \ source

Create block-input   A, A, A, A, A,

: load  ( i*x u -- j*x ) \ block
    \g Text-interpret block @i{u}.  Block 0 cannot be @code{load}ed.
    dup 0= -35 and throw
    block-input 0 new-tib dup loadline ! blk !  -3 loadfilename# !
    ['] interpret bt-rp0-catch pop-file throw ;

: thru ( i*x n1 n2 -- j*x ) \ block-ext
    \G @code{load} the blocks @i{n1} through @i{n2} in sequence.
    1+ swap ?DO  I load  LOOP ;

: +load ( i*x n -- j*x ) \ gforth
    \G Used within a block to load the block specified as the
    \G current block + @i{n}.
    blk @ + load ;

: +thru ( i*x n1 n2 -- j*x ) \ gforth
    \G Used within a block to load the range of blocks specified as the
    \G current block + @i{n1} thru the current block + @i{n2}.
    1+ swap ?DO  I +load  LOOP ;

: --> ( -- ) \ gforthman- gforth chain
    \G If this symbol is encountered whilst loading block @i{n},
    \G discard the remainder of the block and load block @i{n+1}. Used
    \G for chaining multiple blocks together as a single loadable
    \G unit.  Not recommended, because it destroys the independence of
    \G loading.  Use @code{thru} (which is standard) or @code{+thru}
    \G instead.
    refill drop ; immediate

: block-included ( a-addr u -- ) \ gforth
    \G Use within a block that is to be processed by @code{load}. Save
    \G the current blocks file specification, open the blocks file
    \G specified by @i{a-addr u} and @code{load} block 1 from that
    \G file (which may in turn chain or load other blocks). Finally,
    \G close the blocks file and restore the original blocks file.
    block-fid @ >r block-fid off open-blocks
    1 load block-fid @ close-file throw flush
    r> block-fid ! ;

\ thrown out because it may provide unpleasant surprises - anton
\ : include ( "name" -- )
\     name 2dup dup 3 - /string s" .fb" compare
\     0= IF  block-included  ELSE  included  THEN ;

get-current environment-wordlist set-current
true constant block \ environment- environment
true constant block-ext
set-current

' bye deferred? [IF]
    :noname ( -- ) \ tools-ext
	\ Return control to the host operating system (if any).
	['] flush catch-nobt drop defers bye ; is bye
[ELSE]
    0 warnings !@
    : bye ( -- ) \ tools-ext
	['] flush catch-nobt drop bye ;
    warnings !
[THEN]
