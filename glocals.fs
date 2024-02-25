\ A powerful locals implementation

\ Authors: Anton Ertl, Bernd Paysan, Jens Wilke, Neal Crook
\ Copyright (C) 1995,1996,1997,1998,2000,2003,2004,2005,2007,2011,2012,2013,2014,2015,2016,2017,2018,2019,2020,2021,2022 Free Software Foundation, Inc.

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


\ More documentation can be found in the manual and in
\ http://www.complang.tuwien.ac.at/papers/ertl94l.ps.gz

\ Local variables are quite important for writing readable programs, but
\ IMO (anton) they are the worst part of the standard. There they are very
\ restricted and have an ugly interface.

\ So, we implement the locals wordset, but do not recommend using
\ locals-ext (which is a really bad user interface for locals).

\ We also have a nice and powerful user-interface for locals: locals are
\ defined with

\ { local1 local2 ... }
\ or
\ { local1 local2 ... -- ... }
\ (anything after the -- is just a comment)

\ Every local in this list consists of an optional type specification
\ and a name. If there is only the name, it stands for a cell-sized
\ value (i.e., you get the value of the local variable, not it's
\ address). The following type specifiers stand before the name:

\ Specifier	Type	Access
\ W:		Cell	value
\ W^		Cell	address
\ D:		Double	value
\ D^		Double	address
\ F:		Float	value
\ F^		Float	address
\ C:		Char	value
\ C^		Char	address
\ |             nothing switches to zero-initialized values

\ The local variables are initialized with values from the appropriate
\ stack. In contrast to the examples in the standard document our locals
\ take the arguments in the expected way: The last local gets the top of
\ stack, the second last gets the second stack item etc. An example:

\ : CX* { F: Ar  F: Ai  F: Br  F: Bi -- Cr Ci }
\ \ complex multiplication
\  Ar Br f* Ai Bi f* f-
\  Ar Bi f* Ai Br f* f+ ;

\ There will also be a way to add user types, but it is not yet decided,
\ how. Ideas are welcome.

\ Locals defined in this manner live until (!! see below). 
\ Their names can be used during this time to get
\ their value or address; The addresses produced in this way become
\ invalid at the end of the lifetime.

\ Values can be changed with TO, but this is not recomended (TO is a
\ kludge and words lose the single-assignment property, which makes them
\ harder to analyse).

\ As for the internals, we use a special locals stack. This eliminates
\ the problems and restrictions of reusing the return stack and allows
\ to store floats as locals: the return stack is not guaranteed to be
\ aligned correctly, but our locals stack must be float-aligned between
\ words.

require search.fs
require float.fs
require extend.fs \ for case
require compat/caseext.fs
require sections.fs

User locals-size \ this is the current size of the locals stack
		 \ frame of the current word

: compile-@local ( n -- ) \ gforth-internal compile-fetch-local
 case
    0       of postpone @local0 endof
    1 cells of postpone @local1 endof
    2 cells of postpone @local2 endof
    3 cells of postpone @local3 endof
   ( otherwise ) dup postpone @local# ,
 endcase ;

: compile-f@local ( n -- ) \ gforth-internal compile-f-fetch-local
 case
    0        of postpone f@local0 endof
    1 floats of postpone f@local1 endof
   ( otherwise ) dup postpone f@local# ,
 endcase ;

\ locals stuff needed for control structures

: compile-lp+! ( n -- ) \ gforth	compile-l-p-plus-store
    dup negate locals-size +!
    0 over = if
    else -1 cells  over = if postpone lp-
    else  1 floats over = if postpone lp+
    else  2 floats over = if postpone lp+2
    else postpone lp+!# dup ,
    then then then then drop ;

: adjust-locals-size ( n -- ) \ gforth-internal
    \g sets locals-size to n and generates an appropriate lp+!
    locals-size @ swap - compile-lp+! ;

\ the locals stack grows downwards (see primitives)
\ of the local variables of a group (in braces) the leftmost is on top,
\ i.e. by going onto the locals stack the order is reversed.
\ there are alignment gaps if necessary.
\ lp must have the strictest alignment (usually float) across calls;
\ for simplicity we align it strictly for every group.

slowvoc @
slowvoc on \ we want a linked list for the vocabulary locals
vocabulary locals \ this contains the local variables
' locals >wordlist wordlist-id to locals-list
slowvoc !

: no-post -48 throw ;

' translate-nt >body 2@ swap
' no-post
translate: translate-locals ( takes nt, i.e. result of find-name and find-name-in )

: locals-rec [ ' locals >wordlist ] Literal execute
    dup ['] translate-nt = IF  drop ['] translate-locals  THEN ;

' search-order ' locals-rec 2 recognizer-sequence: rec-locals

: activate-locals   ['] rec-locals is rec-nt ;
: deactivate-locals ['] search-order is rec-nt ;

:noname defers wrap@ ['] rec-nt defer@ deactivate-locals ; is wrap@
:noname is rec-nt defers wrap! ; is wrap!

: alignlp-w ( n1 -- n2 )
    \ cell-align size and generate the corresponding code for aligning lp
    aligned dup adjust-locals-size ;

: alignlp-f ( n1 -- n2 )
    faligned dup adjust-locals-size ;

\ a local declaration group (the braces stuff) is compiled by calling
\ the appropriate compile-pushlocal for the locals, starting with the
\ righmost local; the names are already created earlier, the
\ compile-pushlocal just inserts the offsets from the frame base.

Variable val-part \ contains true before |, false afterwards

: locals, ( addr size -- )
    dup locals-size ! swap ! ;

: compile-pushlocal-w ( a-addr -- ) ( run-time: w -- )
\ compiles a push of a local variable, and adjusts locals-size
\ stores the offset of the local variable to a-addr
    locals-size @ alignlp-w cell+ locals,
    val-part @ IF  postpone false  THEN  postpone >l ;

: compile-pushlocal-f ( a-addr -- ) ( run-time: f -- )
    locals-size @ alignlp-f float+ locals,
    val-part @ IF  postpone 0e  THEN  postpone f>l ;

: 2>l swap >l >l ;
opt: drop postpone swap postpone >l postpone >l ;

: compile-pushlocal-d ( a-addr -- ) ( run-time: w1 w2 -- )
    locals-size @ alignlp-w cell+ cell+ locals,
    val-part @ IF  postpone #0.  THEN  postpone 2>l ;

: compile-pushlocal-c ( a-addr -- ) ( run-time: w -- )
    -1 chars compile-lp+!
    locals-size @ swap !
    val-part @ IF  postpone false  THEN  postpone lp@ postpone c! ;

: compile-pushlocal-[ ( size a-addr -- ) ( run-time: addr -- )
    swap maxaligned dup negate compile-lp+!
    val-part @ IF  drop  ELSE  postpone lp@ lit, postpone move  THEN
    locals-size @ swap ! ;

\ locals list operations

[IFUNDEF] >link ' noop Alias >link [THEN]
[IFUNDEF] >f+c  : >f+c cell+ ;     [THEN]

: list-length ( list -- u )
    0 swap begin ( u1 list1 )
       dup while
           name>link 1 under+
    repeat
    drop ;

: /list ( list1 u -- list2 )
    \ list2 is list1 with the first u elements removed
    0 ?do
	name>link
    loop ;

: common-list ( list1 list2 -- list3 ) \ gforth-internal
    \ list3 is the largest common tail of both lists.
    over list-length over list-length - dup 0< if
	negate >r swap r>
    then ( long short u )
    rot swap /list
    begin ( list3 list4 )
	2dup u<> while
	    name>link swap name>link
    repeat
    drop ;

: sub-list? ( list1 list2 -- f ) \ gforth-internal
    \ true iff list1 is a sublist of list2
    over list-length over list-length swap - 0 max /list = ;

: list-size ( list -- u ) \ gforth-internal
    \ size of the locals frame represented by list
    0 ( list n )
    begin
	over 0<>
    while
	over
	((name>)) >body @ max
	swap name>link swap ( get next )
    repeat
    faligned nip ;

Defer locals-list!
:noname locals-list ! ; is locals-list!

: set-locals-size-list ( list -- )
    dup locals-list!
    list-size locals-size ! ;

: check-begin ( list -- )
\ warn if list is not a sublist of locals-list
 locals-list @ sub-list? 0= if
   \ !! print current position
     >stderr ." compiler was overly optimistic about locals at a BEGIN" cr
   \ !! print assumption and reality
 then ;

(field) locals-name-size+ hmsize cell+ , \ fields + wiggle room, name size must be added

: create-local1 ( "name" -- a-addr )
    create
    immediate restrict
    here 0 , ( place for the offset ) ;

16384 extra-section locals-headers

' where, >code-address dodefer: = [IF]
    : locals-where, ( nt -- )
	dup ['] locals-headers @ 2@ swap bounds within
	IF  defers where,  ELSE  drop  THEN ;
    ' locals-where, is where,
[THEN]

: create-local ( "name" -- a-addr )
    \ defines the local "name"; the offset of the local shall be
    \ stored in a-addr
    nextname$ $@ d0= IF
	parse-name nextname THEN
    ['] xt-location defer@ >r ['] noop is xt-location
    ['] create-local1 locals-headers
    r> is xt-location ;

: lp-offset ( n1 -- n2 )
\ converts the offset from the frame start to an offset from lp and
\ i.e., the address of the local is lp+locals_size-offset
  locals-size @ swap - ;

: lp-offset, ( n -- )
\ converts the offset from the frame start to an offset from lp and
\ adds it as inline argument to a preceding locals primitive
  lp-offset , ;

: c+! ( c addr -- ) dup >r c@ + r> c! ;
: 2+! ( d addr -- ) dup >r 2@ d+ r> 2! ;

Create 2!-table ' 2! , ' 2+! ,
Create c!-table ' c! , ' c+! ,
: to-w: ( -- ) -14 throw ;
to-opt: !!?addr!! POSTPONE laddr# >body @ lp-offset, !-table to-!, ;
: to-d: ( -- ) -14 throw ;
to-opt: !!?addr!! POSTPONE laddr# >body @ lp-offset, 2!-table to-!, ;
: to-c: ( -- ) -14 throw ;
to-opt: !!?addr!! POSTPONE laddr# >body @ lp-offset, c!-table to-!, ;
: to-f: ( -- ) -14 throw ;
to-opt: !!?addr!! POSTPONE laddr# >body @ lp-offset, f!-table to-!, ;

: defer@-xt: ( -- ) -14 throw ;
defer@-opt: ( xt -- ) POSTPONE laddr# >body @ lp-offset, postpone @ ;

: val-part-off ( -- ) val-part off ;

vocabulary locals-types \ this contains all the type specifyers, -- and }
locals-types definitions

: W: ( compilation "name" -- a-addr xt; run-time x -- ) \ gforth w-colon
    \G Define value-flavoured cell local @i{name} @code{( -- x1 )}
    create-local ['] to-w: set-to
    \ xt produces the appropriate locals pushing code when executed
    ['] compile-pushlocal-w
  does> ( Compilation: -- ) ( Run-time: -- w )
    \ compiles a local variable access
    @ lp-offset compile-@local ;

: W^ ( compilation "name" -- a-addr xt; run-time x -- ) \ gforth w-caret
    \G Define variable-flavoured cell local @i{name} @code{( -- a-addr )}
    create-local
    ['] compile-pushlocal-w
  does> ( Compilation: -- ) ( Run-time: -- w )
    postpone laddr# @ lp-offset, ;

: F: ( compilation "name" -- a-addr xt; run-time r -- ) \ gforth f-colon
    \G Define value-flavoured float local @i{name} @code{( -- r1 )}
    create-local ['] to-f: set-to
    ['] compile-pushlocal-f
  does> ( Compilation: -- ) ( Run-time: -- r1 )
    @ lp-offset compile-f@local ;

: F^ ( compilation "name" -- a-addr xt; run-time r -- ) \ gforth f-caret
    \G Define variable-flavoured float local @i{name} @code{( -- f-addr )}
    W^ drop ['] compile-pushlocal-f ;

: D: ( compilation "name" -- a-addr xt; run-time x1 x2 -- ) \ gforth d-colon
    \G Define value-flavoured double local @i{name} @code{( -- x3 x4 )}
    create-local ['] to-d: set-to
    ['] compile-pushlocal-d
  does> ( Compilation: -- ) ( Run-time: -- x3 x4 )
    postpone laddr# @ lp-offset, postpone 2@ ;

: D^ ( compilation "name" -- a-addr xt; run-time x1 x2 -- ) \ gforth d-caret
    \G Define variable-flavoured double local @i{name} @code{( -- a-addr )}
    W^ drop ['] compile-pushlocal-d ;

: C: ( compilation "name" -- a-addr xt; run-time c -- ) \ gforth c-colon
    \G Define value-flavoured char local @i{name} @code{( -- c1 )}
    create-local ['] to-c: set-to
    ['] compile-pushlocal-c
  does> ( Compilation: -- ) ( Run-time: -- c1 )
    postpone laddr# @ lp-offset, postpone c@ ;

: C^ ( compilation "name" -- a-addr xt; run-time c -- ) \ gforth c-caret
    \G Define variable-flavoured char local @i{name} @code{( -- c-addr )}
    W^ drop ['] compile-pushlocal-c ;

: XT: ( compilation "name" -- a-addr xt; run-time xt1 -- ) \ gforth x-t-colon
    \G Define defer-flavoured cell local @i{name} @code{( ... -- ... )}
    create-local  ['] to-w: set-to  ['] defer@-xt: set-defer@
    ['] compile-pushlocal-w
  does> ( Compilation: -- ) ( Run-time: .. -- .. )
    @ lp-offset compile-@local postpone execute ;

Defer default: ' W: is default:

:noname ( c-addr u1 "name" -- a-addr xt ) \ gforth <local>bracket (unnamed)
    W^ drop ['] compile-pushlocal-[ ;

: | ( -- ) \ gforth bar
    \G Locals defined behind @code{|} are not initialized from the
    \G stack; so the run-time of words like @code{W:} changes to
    \G @code{( -- )}.
    val-part on ['] val-part-off ;

\ you may want to make comments in a locals definitions group:
synonym \ \ ( compilation 'ccc<newline>' -- ; run-time -- )
\ The actual documentation is in kernel/int.fs

synonym ( ( ( compilation 'ccc<close-paren>' -- ; run-time -- )
\ The actual documentation is in kernel/int.fs

forth definitions
also locals-types

\ these "locals" are used for comparison in TO/create associated vts
c: some-clocal 2drop
d: some-dlocal 2drop
f: some-flocal 2drop
w: some-wlocal 2drop
xt: some-xtlocal 2drop

\ these "locals" create the associated vts
w^ some-waddr 2drop

\ the following gymnastics are for declaring locals without type specifier.
\ we use a catch-all recognizer to do t' new-locals-rec  hat

>r
: new-locals-rec ( caddr u -- [size] nfa )
\ this is the find method of the new-locals vocabulary
\ make a new local with name caddr u; w is ignored
\ the returned nfa denotes a word that produces what W: produces
\ !! do the whole thing without nextname
    2dup nextname
    + 1- c@ '[' = IF
	forth-recognizer stack> >r
	']' parse evaluate
	r> forth-recognizer >stack
	[ r> ] Literal
    ELSE  ['] default: defer@  THEN  nt>rec ;
previous

' new-locals-rec  ' locals-types >wordlist 2 recognizer-sequence: new-locals

Variable {lastnt}

\ and now, finally, the user interface words
: { ( -- hmaddr u latest latestnt wid 0 ) \ gforth open-brace
    \G Start locals definitions.  The Forth-2012 standard name for this
    \G word is @code{@{:}.
    ( >docolloc ) hmsave \ as locals will mess with their own hmtemplate
    latest latestnt dup {lastnt} ! get-current
    ['] new-locals ['] forth-recognize defer@ >stack
   ['] locals >wordlist set-current
    val-part off
    0 postpone [ ; immediate

synonym {: { ( -- hmaddr u latest latestnt wid 0 ) \ local-ext open-brace-colon
\G Start locals definitions.

locals-types definitions

: } ( hmaddr u latest latestnt wid 0 xt1 ... xtn -- ) \ gforth close-brace
    \G Ends locals definitions.  The Forth-2012 standard name for this
    \G word is @code{:@}}.
    ] {lastnt} @ lastnt !
    ['] forth-recognize defer@ stack> drop
    begin
	dup
    while
	execute
    repeat
    drop hm,
    locals-size @ alignlp-f locals-size ! \ the strictest alignment
    set-current lastnt ! last !
    hmrestore
    activate-locals ;

synonym :} } ( hmaddr u latest latestnt wid 0 xt1 ... xtn -- ) \ gforth colon-close-brace
\g Ends locals definitions.

: -- ( hmaddr u latest latestnt wid 0 ... -- ) \ locals- gforth dash-dash
    \G During locals definitions everything from @code{--} to
    \G @code{:@}} is ignored.  This is typically used when you want to
    \G make a locals definition serve double duty as a stack effect
    \G description.
    }
    BEGIN '}' parse dup WHILE
        + 1- c@ dup bl = swap ':' = or UNTIL
    ELSE 2drop THEN ;

forth definitions

\ A few thoughts on automatic scopes for locals and how they can be
\ implemented:

\ We have to combine locals with the control structures. My basic idea
\ was to start the life of a local at the declaration point. The life
\ would end at any control flow join (THEN, BEGIN etc.) where the local
\ is lot live on both input flows (note that the local can still live in
\ other, later parts of the control flow). This would make a local live
\ as long as you expected and sometimes longer (e.g. a local declared in
\ a BEGIN..UNTIL loop would still live after the UNTIL).

\ The following example illustrates the problems of this approach:

\ { z }
\ if
\   { x }
\ begin
\   { y }
\ [ 1 cs-roll ] then
\   ...
\ until

\ x lives only until the BEGIN, but the compiler does not know this
\ until it compiles the UNTIL (it can deduce it at the THEN, because at
\ that point x lives in no thread, but that does not help much). This is
\ solved by optimistically assuming at the BEGIN that x lives, but
\ warning at the UNTIL that it does not. The user is then responsible
\ for checking that x is only used where it lives.

\ The produced code might look like this (leaving out alignment code):

\ >l ( z )
\ ?branch <then>
\ >l ( x )
\ <begin>:
\ >l ( y )
\ lp+!# 8 ( RIP: x,y )
\ <then>:
\ ...
\ lp+!# -4 ( adjust lp to <begin> state )
\ ?branch <begin>
\ lp+!# 4 ( undo adjust )

\ The BEGIN problem also has another incarnation:

\ AHEAD
\ BEGIN
\   x
\ [ 1 CS-ROLL ] THEN
\   { x }
\   ...
\ UNTIL

\ should be legal: The BEGIN is not a control flow join in this case,
\ since it cannot be entered from the top; therefore the definition of x
\ dominates the use. But the compiler processes the use first, and since
\ it does not look ahead to notice the definition, it will complain
\ about it. Here's another variation of this problem:

\ IF
\   { x }
\ ELSE
\   ...
\ AHEAD
\ BEGIN
\   x
\ [ 2 CS-ROLL ] THEN
\   ...
\ UNTIL

\ In this case x is defined before the use, and the definition dominates
\ the use, but the compiler does not know this until it processes the
\ UNTIL. So what should the compiler assume does live at the BEGIN, if
\ the BEGIN is not a control flow join? The safest assumption would be
\ the intersection of all locals lists on the control flow
\ stack. However, our compiler assumes that the same variables are live
\ as on the top of the control flow stack. This covers the following case:

\ { x }
\ AHEAD
\ BEGIN
\   x
\ [ 1 CS-ROLL ] THEN
\   ...
\ UNTIL

\ If this assumption is too optimistic, the compiler will warn the user.

\ Implementation:

\ explicit scoping

:noname ( wid -- )
    dead-code @ IF
	set-locals-size-list
    ELSE
	locals-list @ common-list
	dup list-size adjust-locals-size
	locals-list!
    THEN ;
is adjust-locals-list

\ adapt the hooks

: locals-:-hook ( sys -- sys addr xt n )
    \ addr is the nfa of the defined word, xt its xt
    DEFERS :-hook
    ['] here locals-headers latest latestnt
    clear-leave-stack
    0 locals-size !
    0 locals-list!
    dead-code off
    defstart ;

[ifundef] ->here
    : ->here dp ! ;
[then]

: locals-;-hook ( sys addr xt sys -- sys )
    ?struc
    deactivate-locals
    lastnt ! last ! ['] ->here locals-headers
    DEFERS ;-hook ;

\ THEN (another control flow from before joins the current one):
\ The new locals-list is the intersection of the current locals-list and
\ the orig-local-list. The new locals-size is the (alignment-adjusted)
\ size of the new locals-list. The following code is generated:
\ lp+!# (current-locals-size - orig-locals-size)
\ <then>:
\ lp+!# (orig-locals-size - new-locals-size)

\ Of course "lp+!# 0" is not generated. Still this is admittedly a bit
\ inefficient, e.g. if there is a locals declaration between IF and
\ ELSE. However, if ELSE generates an appropriate "lp+!#" before the
\ branch, there will be none after the target <then>.

: (then-like) ( orig -- )
    dead-orig =
    if
	>resolve 2drop
    else
        dead-code @
        if
	    >resolve set-locals-size-list dead-code off
	else \ both live
	    over list-size adjust-locals-size
	    >resolve
	    adjust-locals-list
	then
	pop-stack-state
    then ;

: (begin-like) ( -- )
    defers begin-like
    dead-code @ if
	\ set up an assumption of the locals visible here.  if the
	\ users want something to be visible, they have to declare
	\ that using ASSUME-LIVE
	backedge-locals @ set-locals-size-list
    then
    dead-code off ;

\ AGAIN (the current control flow joins another, earlier one):
\ If the dest-locals-list is not a subset of the current locals-list,
\ issue a warning (see below). The following code is generated:
\ lp+!# (current-local-size - dest-locals-size)
\ branch <begin>

: (again-like) ( stack-state locals-list addr -- stack-state addr )
    over list-size adjust-locals-size
    swap check-begin  POSTPONE unreachable ;

\ UNTIL (the current control flow may join an earlier one or continue):
\ Similar to AGAIN. The new locals-list and locals-size are the current
\ ones. The following code is generated:
\ ?branch-lp+!# <begin> (current-local-size - dest-locals-size)

: (until-like) ( stack-state list addr xt1 xt2 -- )
    \ list and addr are a fragment of a cs-item
    \ xt1 is the conditional branch without lp adjustment, xt2 is with
    >r >r
    locals-size @ third list-size - dup if ( list dest-addr adjustment )
	r> drop r> compile,
	swap <resolve ( list adjustment ) ,
    else ( list dest-addr adjustment )
	drop
	r> compile, <resolve
	r> drop
    then ( list )
    check-begin pop-stack-state ;

: (exit-like) ( -- )
    0 adjust-locals-size ;

' locals-:-hook IS :-hook
' locals-;-hook IS ;-hook
[ifdef] 0-adjust-locals-size
    :noname 0 adjust-locals-size ; is 0-adjust-locals-size
[then]
[ifdef] colon-sys-xt-offset
colon-sys-xt-offset 4 + to colon-sys-xt-offset
[then]

' (then-like)  IS then-like
' (begin-like) IS begin-like
' (again-like) IS again-like
' (until-like) IS until-like
' (exit-like)  IS exit-like

\ The words in the locals dictionary space are not deleted until the end
\ of the current word. This is a bit too conservative, but very simple.

\ There are a few cases to consider: (see above)

\ after AGAIN, AHEAD, EXIT (the current control flow is dead):
\ We have to special-case the above cases against that. In this case the
\ things above are not control flow joins. Everything should be taken
\ over from the live flow. No lp+!# is generated.

\ About warning against uses of dead locals. There are several options:

\ 1) Do not complain (After all, this is Forth;-)

\ 2) Additional restrictions can be imposed so that the situation cannot
\ arise; the programmer would have to introduce explicit scoping
\ declarations in cases like the above one. I.e., complain if there are
\ locals that are live before the BEGIN but not before the corresponding
\ AGAIN (replace DO etc. for BEGIN and UNTIL etc. for AGAIN).

\ 3) The real thing: i.e. complain, iff a local lives at a BEGIN, is
\ used on a path starting at the BEGIN, and does not live at the
\ corresponding AGAIN. This is somewhat hard to implement. a) How does
\ the compiler know when it is working on a path starting at a BEGIN
\ (consider "{ x } if begin [ 1 cs-roll ] else x endif again")? b) How
\ is the usage info stored?

\ For now I'll resort to alternative 2. When it produces warnings they
\ will often be spurious, but warnings should be rare. And better
\ spurious warnings now and then than days of bug-searching.

\ Explicit scoping of locals is implemented by cs-pushing the current
\ locals-list and -size (and an unused cell, to make the size equal to
\ the other entries) at the start of the scope, and restoring them at
\ the end of the scope to the intersection, like THEN does.


\ And here's finally the ANS standard stuff

: (local) ( addr u -- ) \ local paren-local-paren
    \ a little space-inefficient, but well deserved ;-)
    \ In exchange, there are no restrictions whatsoever on using (local)
    \ as long as you use it in a definition
    dup
    if
	nextname POSTPONE { [ also locals-types ] W: } [ previous ]
    else
	2drop
    endif ;

[ifundef] >extra
    : >extra ( nt -- addr )
        >namehm @ >hmextra ;
[endif]

: >definer ( xt -- definer ) \ gforth
    \G @var{Definer} is a unique identifier for the way the @var{xt}
    \G was defined.  Words defined with different @code{does>}-codes
    \G have different definers.  The definer can be used for
    \G comparison and in @code{definer!}.
    dup >code-address case
        dodoes:     of >extra @ >body 1 or endof
        do;abicode: of >extra @ >body 2 or endof
        nip dup
    endcase ;

: definer! ( definer xt -- ) \ gforth-obsolete
    \G The word represented by @var{xt} changes its behaviour to the
    \G behaviour associated with @var{definer}.
    over 3 and case
        0 of code-address! endof
        1 of swap 3 invert and swap does-code! endof
        2 of swap 3 invert and swap
            do;abicode: any-code! ['] ;abi-code, set-optimizer endof
        -12 throw
    endcase ;

: locals| ( ... "name ..." -- ) \ local-ext locals-bar
    \ don't use 'locals|'! use '{'! A portable and free '{'
    \ implementation is compat/anslocals.fs
    BEGIN
	name 2dup s" |" str= 0=
    WHILE
	(local)
    REPEAT
    drop 0 (local) ; immediate restrict


\ POSTPONEing locals

:noname ( locals-nt -- )
    dup name>int >does-code case
	[ ' some-clocal  >does-code ] literal of name-compsem postpone lit, endof
	[ ' some-dlocal  >does-code ] literal of name-compsem postpone 2lit, endof
	[ ' some-flocal  >does-code ] literal of name-compsem postpone flit, endof
	[ ' some-wlocal  >does-code ] literal of name-compsem postpone lit, endof
	[ ' some-xtlocal >does-code ] literal of >body @ lp-offset compile-@local postpone compile, endof
	[ ' some-waddr   >does-code ] literal of no-post   endof
	>r lit, postpone name-compsem r>
    endcase ;
' translate-locals >body 2 cells + ! \ replace stub
