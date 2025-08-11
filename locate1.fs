\ SwiftForth-like locate etc.

\ Authors: Anton Ertl, Bernd Paysan, Gerald Wodni
\ Copyright (C) 2016,2017,2018,2019,2020,2021,2022,2023,2024 Free Software Foundation, Inc.

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

require status-line.fs

$variable where-results
\ addresses in WHERES that contain the results of the last WHERE
variable where-index -1 where-index !
variable backtrace-index -1 backtrace-index !

-1 0 set-located-view

variable included-file-buffers
\ Bernd-array of c-addr u descriptors for read-only buffers that
\ contain the contents of the included files (same index as
\ included-files); filled on demand and cleared on session end.
:noname ( -- )
    included-file-buffers off defers 'image ; is 'image

: included-buffer ( u -- c-addr u2 )
    \ u is the index into included-files, c-addr u2 describes a buffer
    \ containing the content of the file, or 0 0, if the file cannot
    \ be read.
    dup *terminal*# = IF  drop 0 0  EXIT  THEN \ special files
    dup >r included-file-buffers $[] dup
    >r $@ dup IF  rdrop rdrop  EXIT  THEN  2drop
    r'@ included-files $[]@ r@
    [: >r open-fpath-file throw 2drop r> $slurp ;] catch-nobt IF
	drop 2drop 0 0  r> $free rdrop  EXIT  THEN
    r> $@ rdrop ;

: view>buffer ( view -- c-addr u )
    view>filename# included-buffer ;

: set-bn-view ( -- )
    bn-view @ view>filename# located-top @ 0 encode-view bn-view ! ;

: locate-line {: c-addr1 u1 lineno -- c-addr2 u2 lineno+1 c-addr1 u3 :}
    \ c-addr1 u1 is the rest of the file, c-addr1 u3 the line, and
    \ c-addr2 u2 the rest of the file without the line
    u1 0 u+do
	c-addr1 u1 i /string s\" \r\l" string-prefix? if
	    c-addr1 u1 i 2 + /string lineno 1+ c-addr1 i unloop exit then
	c-addr1 i + c@ dup #lf = swap #cr = or if
	    c-addr1 u1 i 1 + /string lineno 1+ c-addr1 i unloop exit then
    loop
    c-addr1 u1 + 0 lineno 1+ c-addr1 u1 ;

: locate-next-line ( c-addr1 u1 lineno -- c-addr2 u2 lineno+1 )
    locate-line 2drop ;

?: type-prefix ( c-addr1 u1 u -- c-addr2 u2 )
    \ type the u-len prefix of c-addr1 u1, c-addr2 u2 is the rest
    >r 2dup r> umin tuck type safe/string ;

Variable locate-lines#

: locate-lines+ ( c-addr u -- )
    cols x-lines 1+ locate-lines# +! ;

: located-diff ( -- n )
    located-bottom @ located-top @ - ;

: locate-type ( c-addr u lineno -- )
    >r located-diff locate-lines# @ - 0 max cols x-maxlines
    2dup locate-lines+
    r> cr located-view @ view>line = if
	info-color  located-view @ view>char type-prefix
	error-color located-len @            type-prefix
	info-color  type
	default-color exit
    then
    type ;

: locate-print-line ( c-addr1 u1 lineno -- c-addr2 u2 lineno+1 )
    dup >r locate-line r> locate-type ;

: located-buffer ( -- c-addr u )
    located-view @ view>buffer ;

: current-location?1 ( -- f )
    located-view @ -1 = if
        true [: ." no current location" ;] ?warning true exit then
    false ;

: current-location? ( -- )
    ]] current-location?1 ?exit [[ ; immediate

: view>filename ( view -- c-addr u ) \ gforth-internal
    \G filename of view (obtained by @code{name>view})
    view>filename# loadfilename#>str ;

: print-locate-header ( -- )
    status-color
    located-view @ view>filename
    [: type ': emit located-top @ 0 dec.r ;] $tmp
    2dup cols x-lines dup 0> IF
	drop 2drop located-view @ view>filename shorten-file
	[: type ': emit located-top @ 0 dec.r ;] $tmp
	2dup cols x-lines  THEN
    locate-lines# ! type
    default-color ;

: l2 ( -- c-addr u lineno )
    located-buffer 1 case ( c-addr u lineno1 )
	over 0= ?of endof
	dup located-bottom @ >= ?of endof
	locate-lines# @ located-diff >= ?of endof
	dup located-top @ >= ?of locate-print-line contof
	locate-next-line
    next-case ;

: located-erase ( -- )
    locate-lines# @ cursor-previous-line 0 erase-display ;

: display-locate-lines {: utop ubottom -- :}
    located-erase
    utop located-top !
    ubottom located-bottom !
    print-locate-header l2 drop ( located-bottom ! ) 2drop ;
    
: prepend-locate-lines ( u -- )
    \ insert the u lines before the last locate display
    located-top @ swap - 1 max located-bottom @ over rows + 1- min
    display-locate-lines ;

: append-locate-lines ( u -- )
    \ show the u lines after the last locate display, possibly
    \ scrolling away earlier stuff
    >r
    located-bottom @ r@ + dup rows - 1+
    located-top @ locate-lines# @ + r> + rows - 1+
    located-top @ max max
    swap
    display-locate-lines ;

Defer after-l ' noop is after-l

Defer index++
Defer index--
: no-</> ( -- )
    ['] noop is index++
    ['] noop is index-- ;
no-</>

: l1 ( -- )
    l2 dup located-bottom ! after-l 2drop drop no-</> ;

: l ( -- ) \ gforth
    \g Display source code lines at the current location.
    current-location? cr print-locate-header l1 ;

: name-set-located-view ( nt -- )
    dup name>view swap name>string nip set-located-view ;

: xt-locate ( nt/xt -- ) \ gforth
    \g Show the source code of the word @i{xt} and set the current
    \g location there.
    name-set-located-view l ;

: .rec'-stack ( xt -- xt )
    rec'[] $[]# 0 ?DO
	I rec'[] $[] @ ?dup-IF  cr ." Recognized by "
	    2dup = >r
	    dup name>string dup 0= IF  2drop >voc name>string
		dup IF  ." vocabulary "
		    r@ IF  status-color  ELSE  info-color  THEN  type
		ELSE  2drop ." ???"  THEN
	    ELSE
		rot dup >code-address dodefer: = IF  defer@  THEN
		>does-code ['] recognize = IF  ." sequence "  THEN
		r@ IF  status-color  ELSE  info-color  THEN  type
	    THEN
	    rdrop default-color
	THEN
    LOOP ;

: locate ( "name" -- ) \ gforth
    \g Show the source code of the word @i{name} and set the current
    \g location there.
    view' .rec'-stack ?found xt-locate ;

' locate alias view ( "name" -- )

: n ( -- ) \ gforth
    \g Display lines behind the current location, or behind the last
    \g @code{n} or @code{b} output (whichever was later).
    current-location?
    located-bottom @ dup located-top ! rows 2/ + located-bottom !
    set-bn-view cr print-locate-header l1 ;

: b ( -- ) \ gforth
    \g Display lines before the current location, or before the last
    \g @code{n} or @code{b} output (whichever was later).
    current-location?
    located-top @ dup located-bottom ! rows 2/ - 0 max located-top !
    set-bn-view cr print-locate-header l1 ;

: extern-g ( -- ) \ gforth-internal
    \g Enter the external editor at the place of the latest error,
    \g @code{locate}, @code{n} or @code{b}.
    current-location?
    bn-view @ ['] editor-cmd >string-execute 2dup system drop free
    throw ;

Defer g ( -- ) \ gforth
    \g Enter the editor at the current location, or at the start of
    \g the last @code{n} or @code{b} output (whichever was later).
' extern-g is g

: edit ( "name" -- ) \ gforth
    \g Enter the editor at the location of "name"
    (') name-set-located-view g ;


\ avoid needing separate edit and locate words for the rest

defer l|g ( -- )
\ either do l or g, and then possibly change l|g

variable next-l|g ( -- addr )

: l-once ( -- )
    l next-l|g @ is l|g ;

: ll ( -- ) \ gforth
    \g The next @code{ww}, @code{nw}, @code{bw}, @code{bb}, @code{nb},
    \g @code{lb} (but not @code{locate}, @code{edit}, @code{l} or
    \g @code{g}) displays in the Forth system (like @code{l}).  Use
    \g @code{ll ll} to make this permanent rather than one-shot.
    `l|g defer@ next-l|g !
    `l-once is l|g ;

: g-once ( -- )
    g next-l|g @ is l|g ;

: gg ( -- ) \ gforth
    \g The next @code{ww}, @code{nw}, @code{bw}, @code{bb}, @code{nb},
    \g @code{lb} (but not @code{locate}, @code{edit}, @code{l} or
    \g @code{g}) puts it result in the editor (like @code{g}).  Use
    \g @code{gg gg} to make this permanent rather than one-shot.
    `l|g defer@ next-l|g !
    `g-once is l|g ;

ll ll \ set default to use L


\ backtrace locate stuff:

\ an alternative implementation of much of this stuff is elsewhere.
\ The following implementation works for code in sections, too, but
\ currently does not survive SAVESYSTEM.
0 [if]
256 1024 * constant bl-data-size

0
2field:  bl-bounds
field:   bl-next
bl-data-size cell+ +field bl-data
constant bl-size

variable code-locations 0 code-locations !

: .bl {: bl -- :}
    cr bl bl-bounds 2@ swap 16 hex.r 17 hex.r
    bl bl-data 17 hex.r
    bl bl-next @ 17 hex.r ;

: .bls ( -- )
    cr ."       code-start         code-end          bl-data          bl-next"
    code-locations @ begin
	dup while
	    dup .bl
	    bl-next @ repeat
    drop ;

: addr>view ( addr -- view|0 )
    code-locations @ begin ( addr bl )
	dup while
	    2dup bl-bounds 2@ within if
		tuck bl-bounds 2@ drop  - + bl-data @ exit then
	    bl-next @ repeat
    2drop 0 ;

: xt-location2 ( addr bl -- addr )
    \ knowing that addr is within bl, record the current source
    \ position for addr
    2dup bl-bounds 2@ drop - + bl-data ( addr addr' )
    current-sourceview swap 2dup ! cell+ ! ;

: new-bl ( addr blp -- )
    bl-size allocate throw >r
    swap dup bl-data-size + r@ bl-bounds 2!
    dup @ r@ bl-next !
    r@ bl-data bl-data-size cell+ erase
    r> swap ! ;
    
: xt-location1 ( addr -- addr )
    code-locations begin ( addr blp )
	dup @ 0= if
	    2dup new-bl then
	@ 2dup bl-bounds 2@ within 0= while ( addr bl )
	    bl-next repeat
    xt-location2 ;

' xt-location1 is xt-location
[then]

: bt-location ( u -- f )
    \ locate-setup backtrace entry with index u; returns true iff successful
    cells >r stored-backtrace $@ r@ u> if ( addr1 r: offset )
        r> + @ dup addr>view dup if ( x view )
            swap >bt-entry dup if
                name>string nip then
	    1 max set-located-view true exit then
    else
        drop rdrop then
    drop ." no location for this backtrace index" false ;

: backtrace# ( -- n ) stored-backtrace $@len cell/ ;

: backtrace++ ( -- n )
    backtrace-index @ 1+
    dup backtrace# = if  drop 0  then ;

: backtrace-- ( -- n )
    backtrace-index @ dup 0<= if  drop backtrace#  then
    1- ;

: bt-</> ( -- )
    [: backtrace# 0 ?DO  backtrace++ dup backtrace-index ! bt-location ?LEAVE
	LOOP ;] is index++
    [: backtrace# 0 ?DO  backtrace-- dup backtrace-index ! bt-location ?LEAVE
	LOOP ;] is index-- ;

: tt ( u -- ) \ gforth
    bt-</>
    dup backtrace-index !
    bt-location if
        l|g
    else
        -1 backtrace-index ! then ;

: nt (  -- ) \ gforth
    backtrace++ tt ;

: bt ( -- ) \ gforth
    backtrace-- tt ; 

\ where

: unbounds ( c-start c-end -- c-start u )
    over - 0 max ;

: type-notabs ( c-addr u -- )
    \ like type, but type a space for each tab
    bounds ?do
        i c@ dup #tab = if drop bl then emit loop ;

: width-type ( c-addr u uwidth -- uwidth1 )
    \ type the part of the string that fits in uwidth; uwidth1 is the
    \ remaining width; replaces tabs with spaces
    1 swap x-maxlines+rest >r type r> ;

: .wheretype1 ( c-addr u view urest -- )
    { urest } view>char >r -trailing over r> + { c-pos } 2dup + { c-lineend }
    (parse-white) drop ( c-addr1 )
    info-color  c-pos unbounds urest width-type ->urest
    error-color c-pos c-lineend unbounds (parse-white) tuck
    urest width-type ->urest
    info-color  c-pos + c-lineend unbounds urest width-type ->urest
    default-color urest spaces ;
    
: .whereline {: view u -- :}
    \ print the part of the source line around view that fits in the
    \ current line, of which u characters have already been used
    view view>buffer
    1 case ( c-addr u lineno1 )
	over 0= ?of endof
	dup view view>line = ?of locate-line view u .wheretype1 endof
	locate-next-line
    next-case
    drop 2drop ;

: .whereview1 ( view wno -- )
    0 <<# `#s #10 base-execute #> rot ( c-addr u view )
    dup .sourceview-width space 1+ third + cols swap - .whereline type #>> ;

: forwheres ( ... xt -- ... )
    where-results $free
    0 { xt wno } wheres $@ where-struct mem+do
	i where-nt @ xt execute if
	    i where-loc @ cr wno .whereview1
	    i where-results >stack
	    1 +>wno
	then
    loop ;

: (where) ( "name" -- ) \ gforth-internal
    (') [: over = ;] forwheres
    drop -1 where-index ! ;

Defer where-setup
: where-reset ( n1 n2 -- ) to source-line#  to source-pos#
    lastfile off ;

: short-where ( -- ) \ gforth
    \G Set up @code{where} to use a short file format (default).
    ['] short~~ is where-setup ;
: expand-where ( -- ) \ gforth
    \G Set up @code{where} to use a fully expanded file format (to
    \G pass to e.g. editors).
    ['] expand~~ is where-setup ;
: prepend-where ( -- ) \ gforth
    \G Set up @code{where} to show the file on a separate line,
    \G followed by @code{where} lines without file names (like
    \G SwiftForth).
    ['] prepend~~ is where-setup ;
short-where

: where ( "name" -- ) \ gforth
    \g Show all places where @i{name} is used (text-interpreted).  You
    \g can then use @code{ww}, @code{nw} or @code{bw} to inspect
    \g specific occurrences more closely.  Gforth's @code{where} does
    \g not show the definition of @i{name}; use @code{locate} for
    \g that.
    ['] noop ['] filename>display
    [: where-setup source-pos# source-line# 2>r
	(where) 2r> where-reset ;] wrap-xt ;

: (ww) ( u -- ) \ gforth-internal
    dup where-index !
    where-results $@ rot cells tuck u<= if
	2drop -1 0 -1 where-index !
    else
        + @ 2@ name>string nip then
    set-located-view ;

: where-index++ ( -- n )
    where-index @ 1+
    dup where-results $@len cell/ = IF  drop 0  THEN ;

: where-index-- ( -- n )
    where-index @ dup 0<= if
	drop where-results $@len cell/  then
    1- ;

: where-</> ( -- )
    [: where-index++ (ww) ;] is index++
    [: where-index-- (ww) ;] is index-- ;

: ww ( u -- ) \ gforth
    \G The next @code{l} or @code{g} shows the @code{where} result
    \G with index @i{u}
    where-</> (ww) l|g ;

: nw ( -- ) \ gforth
    \G The next @code{l} or @code{g} shows the next @code{where}
    \G result; if the current one is the last one, after @code{nw}
    \G there is no current one.  If there is no current one, after
    \G @code{nw} the first one is the current one.
    where-</> where-index++ ww ;

: bw ( -- ) \ gforth
    \G The next @code{l} or @code{g} shows the previous @code{where}
    \G result; if the current one is the first one, after @code{bw}
    \G there is no current one.    If there is no current one, after
    \G @code{bw} the last one is the current one.
    where-</> where-index-- ww ;

\ locate words by pattern

Variable browse-results

: (browse) ( "search" -- )
    context @ wid>words[]
    where-results $free browse-results $free
    parse-name 0 { d: match wno }
    words[] $@ cell MEM-DO
	i @ name>string match mword-match IF
	    { | where[ where-struct ] }
	    i @ where[ where-nt !
	    i @ name>view where[ where-loc !
	    where[ where-loc @ cr wno .whereview1
	    where[ where-struct browse-results $+!
	    1 +>wno
	THEN
    LOOP
    words[] $free
    browse-results $@ where-struct MEM+DO
	i where-results >stack
    LOOP ;

: browse ( "subname" -- ) \ gforth
    \g Show all places where a word with a name that contains
    \g @i{subname} is defined (@code{mwords}-like, @pxref{Word
    \g Lists}).  You can then use @code{ww}, @code{nw} or @code{bw}
    \g (@pxref{Locating uses of a word}) to inspect specific
    \g occurrences more closely.
    ['] noop ['] filename>display
    [: where-setup source-pos# source-line# 2>r
	(browse) 2r> where-reset ;] wrap-xt ;

\ count word usage

: usage# ( nt -- n ) \ gforth-internal
    \G count usage of the word @var{nt}
    0 wheres $@ where-struct MEM+DO
	over i where-nt @ = -
    LOOP  nip ;

\ display unused words

lcount-mask 1+ Constant unused-mask

: .wids ( nt1 .. ntn n ) cr 0 swap 0 ?DO swap .word LOOP drop ;
: +unused ( nt -- )
    >f+c unused-mask over @ or swap ! ;
: -unused ( nt -- )
    >f+c unused-mask invert over @ and swap ! ;
: unused-all ( wid -- )
    [: +unused true ;] swap traverse-wordlist ;
: unmark-used ( -- )
    wheres $@ where-struct MEM+DO
	i where-nt @ dup forthstart here within
	IF  -unused  ELSE  drop  THEN
    LOOP ;
: unused@ ( wid -- nt1 .. ntn n )
    0 [: dup >f+c @ unused-mask and IF
	    dup -unused swap 1+
	ELSE  drop  THEN  true ;]
    rot traverse-wordlist ;
: unused-wordlist ( wid -- )
    dup unused-all unmark-used unused@ .wids ;
: unused-words ( -- ) \ gforth
    \G list all words without usage
    context @ unused-wordlist ;

\ help

s" doc/gforth.txt" add-included-file

included-files $[]# 1- constant doc-file#

: count-lfs ( c-addr u -- u1 )
    0 -rot bounds ?do
        i c@ #lf = - loop ;

: set-help-view ( view charlen endline -- )
    located-bottom !
    located-len ! dup located-view ! dup bn-view !
    view>line
    located-top ! ;

: open-doc ( -- c-addr u )
    doc-file# included-buffer dup 0= if
	cr error-color ." Documentation file not found" default-color
    THEN ;

: help-word {: c-addr u -- :}
    open-doc {: c-addr1 u1 :} u1 if
        c-addr1 u1 c-addr u [: "\l'" type type "' ( " type ;] $tmp
        capssearch if
            {: c-addr3 u3 :} c-addr1 u1 u3 - count-lfs 2 + {: top-line :}
	    top-line doc-file# swap 1 encode-view u
	    c-addr3 u3 2dup "\l\l" search if nip - else 2drop then
	    count-lfs top-line + set-help-view l exit
        else
	    2drop c-addr u cr
	    error-color ." No documentation for " type default-color
	then
    then
    info-color ." , LOCATEing source" default-color
    c-addr u (view') xt-locate ;

: help-section {: c-addr u -- :}
    \ !! implement this!
    c-addr c@ digit? if  drop
	c-addr u [: #lf emit type space ;] $tmp to u to c-addr
    then
    open-doc {: c-addr1 u1 :} u1 if
	c-addr1 u1
	BEGIN
	    c-addr u capssearch WHILE
		1 safe/string {: c-addr3 u3 :}
		c-addr3 u3 #lf scan dup if
		    1 safe/string #lf $split 2swap dup >r
		    '*' skip '=' skip '-' skip nip 0= r> 0<> and if
			2drop
			c-addr1 u1 u3 - count-lfs 1+ {: top-line :}
			top-line doc-file# swap 0 encode-view 0
			c-addr3 u3 2dup
			3 0 ?DO   "\l\l" search if  2 safe/string  then  LOOP
			nip - count-lfs rows 2/ umax rows 1- umin
			top-line + set-help-view l exit
		    then
		then
	REPEAT
	2drop c-addr u #lf skip -trailing cr
	error-color ." No documentation for " type default-color
    then ;

: help ( "rest-of-line" -- ) \ gforth
    \G If no name is given, show basic help.  If a documentation node
    \G name is given followed by "::", show the start of the node.  If
    \G the name of a word is given, show the documentation of the word
    \G if it exists, or its source code if not.  If something else is
    \G given that is recognized, shows help on the recognizer.  You
    \G can then use the same keys and commands as after using
    \G @code{locate} (@pxref{Locating source code definitions}).
    >in @ >r parse-name dup 0= if
        rdrop 2drop basic-help exit then
    drop 0 parse + over - -trailing 2dup s" ::" string-suffix? if
        rdrop 2 - help-section exit then
    r@ >in ! parse-name 2dup (view') ?dup-if
        rdrop nip nip name>string help-word 2drop exit then
    2drop r> >in ! 0 parse 2drop 2drop
    error-color ." Not a section or word" default-color ;

\ whereg

#24 #80 2Constant plain-form

' (type) ' (emit) ' (cr) ' plain-form output: plain-out
: plain-output ( xt -- )
    op-vector @ >r  plain-out  catch  r> op-vector !  throw ;

s" os-type" environment? [IF]
    s" linux-android" string-prefix? 0= [IF]

User sh$  cell uallot drop
: sh-get ( c-addr u -- c-addr2 u2 ) \ gforth
    \G Run the shell command @i{addr u}; @i{c-addr2 u2} is the output
    \G of the command.  The exit code is in @code{$?}, the output also
    \G in @code{sh$ 2@@}.
    sh$ free-mem-var
    r/o open-pipe throw dup >r slurp-fid
    r> close-pipe throw to $? 2dup sh$ 2! ;

:noname '`' parse sh-get ;
:noname '`' parse postpone SLiteral postpone sh-get ;
interpret/compile: s` ( "eval-string" -- addr u )

2variable whereg-filename 0 0 whereg-filename 2!

: delete-whereg ( -- )
    \ delete whereg file
    whereg-filename 2@ dup if
	2dup delete-file throw drop free throw
    else  2drop  then ;

: whereg ( "name" -- ) \ gforth
    \g Like @code{where}, but puts the output in the editor.  In
    \g Emacs, you can then use the compilation-mode commands
    \g (@pxref{Compilation Mode,,,emacs,GNU Emacs Manual}) to inspect
    \g specific occurrences more closely.
    delete-whereg
    s` mktemp /tmp/gforth-whereg-XXXXXX` 1- save-mem 2dup whereg-filename 2!
    2dup r/w open-file throw
    [:  "-*- mode: compilation; default-directory: \"" type
	s` pwd` 1- type
	"\" -*-" type
	['] expand-file ['] filename>display
	[: ['] (where) plain-output ;] wrap-xt
    ;] over outfile-execute close-file throw
    `edit-file-cmd >string-execute 2dup system drop free throw ;

' bye deferred? [IF]
    :noname ( -- ) \ tools-ext
	delete-whereg defers bye ; is bye
[ELSE]
    0 warnings !@
    : bye ( -- ) delete-whereg bye ;
    warnings !
[THEN]
    
    [THEN]
[THEN]

\ fancy after-l

: fancy-after-l ( c-addr1 u1 lineno1 -- c-addr2 u2 lineno2 )
    \ allow to scroll around right after LOCATE and friends:
    case
	ekey \ k-winch will only be visible with ekey
	ctrl p  of 1 prepend-locate-lines contof
	ctrl n  of 1  append-locate-lines contof
	ctrl u  of rows 2/ prepend-locate-lines contof
	ctrl d  of rows 2/  append-locate-lines contof
	'k'     of 1 prepend-locate-lines contof
	'j'     of 1  append-locate-lines contof
	'l'     of located-diff >r  index++
	    r> located-diff - append-locate-lines contof
	'h'     of located-diff >r  index--
	    r> located-diff - append-locate-lines contof
	ctrl b  of rows 2 - prepend-locate-lines contof
	bl      of rows 2 -  append-locate-lines contof
	ctrl l  of 0  append-locate-lines contof
	'q'     of endof
	#esc    of endof
	#cr     of endof
	#lf     of endof
	ekey>xchar ?of  ['] xemit $tmp unkeys  endof
	k-up    of 1 prepend-locate-lines contof
	k-down  of 1  append-locate-lines contof
	k-prior of rows 2/ prepend-locate-lines contof
	k-next  of rows 2/  append-locate-lines contof
	k-winch of 0  append-locate-lines contof
	k-right of located-diff >r  index++
	    r> located-diff - append-locate-lines contof
	k-left  of located-diff >r  index--
	    r> located-diff - append-locate-lines contof
    endcase ;

' fancy-after-l is after-l
