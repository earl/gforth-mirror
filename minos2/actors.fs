\ MINOS2 actors basis

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2017,2018,2019,2020,2021,2022,2023,2024 Free Software Foundation, Inc.

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

\ actors are responding to any events that need to be handled

\ actor handler class

\ platform specific action handler

require ../forward.fs

\ edit actor

edit-terminal-c uclass edit-out
    cell uvar edit$ \ pointer to the edited string
end-class edit-widget-c

edit-widget-c ' new static-a with-allocater Constant edit-widget

false Value grab-move?   \ set to object to grab moves
false value inside-move? \ set to object if touched

:is dispose-check ( o:disposed -- )
    o grab-move?   = IF  false to grab-move?    THEN
    o inside-move? = IF  false to inside-move?  THEN ;

0e FValue tx-sum
0e FValue ty-sum
0e+0ei ZValue gxy-sum
$10 stack: vp'<>

: vp-need-or ( -- )
    \ converge needs between viewport and main need mask
    vp-need @ need-mask @ over $FF and over $FF and or >r
    $-100 and swap $-100 and max r> or need-mask ! ;
: vp-needed<>| ( xt -- )
    vp'<> stack# 0= IF  execute  EXIT  THEN
    need-mask >r
    vp'<> stack# 1- vp'<> $[] @ .vp-need to need-mask
    catch
    0 vp'<> stack# 1- -DO
	I 1- vp'<> $[] @ .vp-need to need-mask
	I    vp'<> $[] @ .vp-need-or
    1 -LOOP
    r> to need-mask
    0 vp'<> $[] @ .vp-need-or
    throw ;

: >grab-move? ( o -- )
    to grab-move?  tx-sum ty-sum to gxy-sum
    vp<> $@ vp'<> $! ;

: >txy ( -- l:tx l:ty )
    tx-sum f>l  ty-sum f>l ;
: txy> ( l:tx l:ty )
    0 f@localn to ty-sum  1 f@localn to tx-sum  lp+ lp+ ;
: dxy$ ( rx ry adest addr u -- )
    { f: dx f: dy } bounds U+DO
	I         sf@ dx f+ sf!+
	I sfloat+ sf@ dy f+ sf!+
    [ 2 sfloats ]L +LOOP drop ;
: >dxy ( $addr -- $addr )
    dup gxy-sum $@ over swap dxy$ ;

[IFDEF] x11      include x11-actors.fs      [ELSE]
[IFDEF] wayland  include wayland-actors.fs  [ELSE]
[IFDEF] android  include android-actors.fs  [THEN] [THEN] [THEN]

\ generic actor stuff

actor class
end-class simple-actor

simple-actor :method clicked { f: rx f: ry b n -- }
    click( o h. caller-w .name$ type space caller-w h. ." simple click: " rx f. ry f. b . n . cr ) ;
simple-actor :method ukeyed ( addr u -- )
    event( o h. caller-w .name$ type space caller-w h. ." keyed: " type cr )else( 2drop ) ;
simple-actor :method ekeyed ( ekey -- )
    event( o h. caller-w .name$ type space caller-w h. ." ekeyed: " h. cr )else( drop ) ;
: .touch ( $xy b -- )
    event( ." touch: " h. $@ bounds ?DO  I sf@ f.  1 sfloats +LOOP cr )else( 2drop ) ;
simple-actor :method touchdown ( $xy b -- )
    event( o h. caller-w .name$ type space caller-w h. ." down " .touch )else( 2drop ) ;
simple-actor :method touchup ( $xy b -- )
    event( o h. caller-w .name$ type space caller-w h. ." up " .touch )else( 2drop ) ;
simple-actor :method touchmove ( $xy b -- )
    event( o h. caller-w .name$ type space caller-w h. ." move " .touch )else( 2drop ) ;
simple-actor :method entered ( -- ) o to inside-move? ;

: simple[] ( o -- o )
    >o simple-actor new !act o o> ;

\ click actor

simple-actor class
    method do-action
    defer: ck-action ( addr -- )
    addressable: value: data
end-class click-actor

' ck-action click-actor is do-action

: click[] ( o xt data -- o )
    \ xt takes ( data -- )
    rot >o click-actor new >o to data is ck-action o o> !act o o> ;

click-actor :method clicked ( rx ry b n -- )
    fdrop fdrop 2 = swap 1 <= and IF
	click( o h. ." is clicked, do-action " action-of ck-action xt-see cr )
	do-action
    THEN ;
click-actor :method ukeyed ( ukeyaddr u -- )
    bounds ?DO  I c@ bl = IF  do-action  THEN
    LOOP ;
click-actor :method ekeyed ( ekey -- )
    k-enter = IF  do-action  THEN ;

\ key actor

click-actor class
end-class key-actor

: key[] ( o xt data -- o )
    \ xt takes ( data -- )
    rot >o key-actor new >o to data is ck-action o o> !act o o> ;

actor action-of clicked key-actor is clicked
key-actor :method ukeyed ( ukeyaddr u -- )
    bounds ?DO  I xc@+ swap I - >r  do-action  r>  +LOOP ;
' do-action key-actor is ekeyed

\ toggle actor

click-actor class
end-class toggle-actor

: toggle[] ( o xt state -- o )
    rot >o toggle-actor new >o to data is ck-action o o> !act o o> ;

toggle-actor :method do-action data 0= dup to data ck-action ;

\ actor for a box with one active element

actor class
end-class box-actor

0 value select-mode    \ 0: chars, 1: words, 2: lines
0 value start-cursize  \ selection helper

: re-focus { c-act -- }
    c-act .active-w ?dup-IF  .act ?dup-IF  .defocus  THEN  THEN
    o c-act >o to active-w o>
    c-act .active-w ?dup-IF  .act ?dup-IF  .focus  THEN  THEN ;

: .parents ( o:widget -- )
    parent-w ?dup-IF  >o recurse o>  THEN  o h. name$ type space ;

: engage ( object -- )
    >o parent-w ?dup-IF
	recurse parent-w .act ?dup-IF  re-focus  THEN  THEN  o> ;

: engage-edit ( addr u object -- )
    dup engage >o tuck to text$ 0 to curpos to cursize o> ;

: box-?inside ( rx ry -- act )
    0 [{: f: rx f: ry :}l
	act ?dup-IF  >o rx ry
	    r@ caller-w >r to caller-w ['] ?inside catch
	    r> to caller-w  throw
	    dup IF  nip  ELSE  drop  THEN o>  THEN ;]
    box-touched# do-childs-act? ;

box-actor :method ?inside ( rx ry -- act )
    fover fover [ actor ] defers ?inside 0= IF  fdrop fdrop 0  EXIT  THEN
    box-?inside ;

box-actor :method clicked ( rx ry b n -- )
    click( o h. caller-w .name$ type space caller-w h. ." box click: " fover f. fdup f. over . dup . cr )
    fover fover ?inside ?dup-IF  .clicked  EXIT  THEN
    2drop fdrop fdrop ;
box-actor :method dndmove ( rx ry -- )
    fover fover ?inside ?dup-IF  .dndmove  EXIT  THEN
    fdrop fdrop ;
box-actor :method dnddrop ( rx ry addr u -- )
    fover fover ?inside ?dup-IF  .dnddrop  EXIT  THEN
    2drop fdrop fdrop ;
box-actor :method scrolled ( axis dir x y -- )
    fover fover ?inside ?dup-IF  .scrolled  EXIT  THEN
    2drop fdrop fdrop ;
box-actor :method ukeyed ( addr u -- )
    active-w ?dup-IF  .act ?dup-IF .ukeyed  EXIT  THEN  THEN  2drop ;
box-actor :method ekeyed ( ekey -- )
    active-w ?dup-IF  .act ?dup-IF .ekeyed  EXIT  THEN  THEN  drop ;
: xy@ ( addr -- rx ry )  $@ drop dup sf@ sfloat+ sf@ ;
box-actor :method touchdown ( $xy b -- )
    over xy@ ?inside ?dup-IF  .touchdown  ELSE  2drop  THEN ;
box-actor :method touchup ( $xy b -- )
    over xy@ ?inside ?dup-IF  .touchup  ELSE  2drop  THEN ;
box-actor :method touchmove ( $xy b -- )
    event( o h. caller-w h. ." box move " 2dup .touch )
    [: over xy@ inside?
	event( o h. caller-w h. ." move inside? " dup . cr )
	IF
	    inside-move? >r
	    act inside-move? <> IF  act .entered  THEN
	    r@ inside-move? <> act and
	    IF  r@ ?dup-IF  .left  THEN  THEN  rdrop
	    2dup act .touchmove  THEN
   ;] box-touched# do-childs-act?
    2drop ;
box-actor :method defocus ( -- )
    [: act .defocus ;] box-defocus# do-childs-act?
    0 to active-w ;
' noop box-actor is entered

: box[] ( o -- o )
    >o box-actor new !act o o> ;

\ scroll actor

box-actor class
    defer: sr-action
end-class scroll-actor

: scroll[] ( o xt -- o )
    \ xt takes ( data -- )
    swap >o scroll-actor new >o is sr-action o o> !act o o> ;

scroll-actor :method clicked ( rx ry b n -- )
    click( o h. ." is clicked, do-action " action-of ck-action xt-see cr )
    over $18 and 0<> over 1 and 0= and IF
	fdrop fdrop 1 and 0= IF  dup
	    >r $08 and IF  -1 sr-action  THEN
	    r@ $10 and IF   1 sr-action  THEN
	    r>
	THEN  drop
    ELSE
	[ box-actor ] defers clicked
    THEN ;
scroll-actor :method ukeyed ( ukeyaddr u -- )
    over ctrl P = >r over ctrl N = r> or over 1 = IF
	bounds ?DO  case I c@
		ctrl P of  -1 sr-action  endof
		ctrl N of   1 sr-action  endof
	    endcase
	LOOP
    ELSE  [ box-actor ] defers ukeyed  THEN ;
scroll-actor :method ekeyed ( ekey -- )
    case
	k-left  of  -1 sr-action  endof
	k-right of   1 sr-action  endof
	[ box-actor ] defers ekeyed 0
    endcase ;

\ scroll through tabs

: find-tab ( -- n )
    -1 0 [: 0 childs[] $[] @ .raise f0=
	IF  nip dup  THEN  1+ ;] caller-w .do-childs drop ;
: set-tab ( n -- )
    dup 0 caller-w .childs[] $[]# within 0= IF  drop  EXIT  THEN
    caller-w .childs[] $[] @ >r s"  " r> .act .ukeyed ;

: tabs[] ( o -- o )
    [: find-tab + set-tab +sync ;] scroll[] ;

\ viewport

box-actor class
    field: txy$ \ translated xy$
    value: ?inside-mode
    fvalue: vmotion-time
    sfvalue: vstart-x
    sfvalue: vstart-y
    sfvalue: vpstart-x
    sfvalue: vpstart-y
    sfvalue: vold-x
    sfvalue: vold-y
    sfvalue: vmotion-dx
    sfvalue: vmotion-dy
    sfvalue: vmotion-dt
end-class vp-actor

: tx ( rx ry -- rx' ry' )
    fswap vp-x         x f- fdup +to tx-sum f+
    fswap vp-h vp-y f- y f- fdup +to ty-sum f+ ;
: tx$ ( $rxy*n -- $rxy*n' )
    0e fdup tx
    dup $@len act .txy$ $!len
    act .txy$ $@ drop swap $@ dxy$  act .txy$ ;

: vp-needed| ( xt -- )
    vp-needed vp-need-or ;

1024 s>f FValue drag-rate \ 1 screen/s²
: actors-init ( -- )
    screen-pwh max s>f to drag-rate ;
:is window-init ( -- )
    defers window-init actors-init ;
ctx [IF] actors-init [THEN]
50m FValue min-dt \ measure over 50ms at least

: vp-setxy ( rx ry -- )
    caller-w >o
    0e fmax vp-h h d f+ f- fmin fround to vp-y
    0e fmax vp-w w f- fmin fround to vp-x
    ?vpt-x ?vpt-y or IF  ['] +sync vp-needed  THEN
    vp-reslide o> +sync ;

: >motion-dt ( -- flag )
    ftime fdup vmotion-time f-
    fdup min-dt f> dup IF
	to vmotion-dt to vmotion-time
    ELSE
	fdrop fdrop
    THEN ;

: vpxy! ( rx ry -- )
    >motion-dt IF
	fdup  vold-y f- to vmotion-dy fdup  to vold-y
	fover vold-x f- to vmotion-dx fover to vold-x
    THEN
    vstart-y f- vpstart-y f+ fswap
    vstart-x fswap f- vpstart-x f+ fswap
    vp-setxy ;

: set-startxy ( -- )
    caller-w >o vp-x vp-y o> to vpstart-y  to vpstart-x ;

: motion-dxy ( -- px )
    vmotion-dx f**2 vmotion-dy f**2 f+ fsqrt ;
: motion-time ( -- time )
    motion-dxy vmotion-dt drag-rate f* f/ ;

: vp-deltaxy ( time -- rx ry )
    fdup  vmotion-dx f* vpstart-x fswap f-
    fswap vmotion-dy f* vpstart-y f+ ;

: vp-motion ( 0..1 addr -- )
    >o fdup f**2 f2/ f-
    motion-dxy vmotion-dt f**2 drag-rate f* f/ f*
    vp-deltaxy vp-setxy o> ;

forward sin-t

: vp-scroll ( 0..1 addr -- )
    >o sin-t fdup to vmotion-dt
    vp-deltaxy vp-setxy o> ;

forward anim-del

vp-actor :method ?inside ( rx ry -- act )
    ?inside-mode
    click( ." vp-inside: " fover f. fdup f. dup . )
    IF    box-?inside
    ELSE  [ actor ] defers ?inside
    THEN
    click( dup h. cr ) ;
: 1-?inside ( rx ry xt -- act )
    1 to ?inside-mode  catch  0 to ?inside-mode  throw ;

Variable last-scrolldir
: vp-scrolling ( dir -- )
    set-startxy
    s>d dup last-scrolldir !@ <> IF  0 to clicks dup abs 1 umax / 2*  THEN
    dup abs 2 = IF
	0e fdup to vmotion-dx
    ELSE
	vmotion-dt vmotion-dy f*
    THEN  to vmotion-dy  0e to vmotion-dt
    o anim-del
    caller-w .h 16 fm*/ +to vmotion-dy
    0.333e o ['] vp-scroll >animate ;

vp-actor :method clicked ( rx ry bmask n -- ) 
    click( o h. caller-w .name$ type space ." vp click" cr )
    grab-move? o <> IF
	fover fover caller-w .inside? 0= IF  2drop fdrop fdrop  EXIT  THEN
    THEN
    over 2 or 2 = IF
	dup 1 and IF  2drop
	    o anim-del
	    fdup to vstart-y  fover to vstart-x
	    to vold-y  to vold-x
	    ftime to vmotion-time
	    0e to vmotion-dt
	    set-startxy
	    o >grab-move?  EXIT
	ELSE
	    grab-move? o = IF  2drop vpxy!
		false to grab-move?
		vmotion-dt f0> motion-dxy f0> and IF
		    set-startxy
		    >motion-dt drop
		    motion-time
		    o anim-del
		    o ['] vp-motion >animate
		THEN  EXIT  THEN
	THEN
    THEN
    over $18 and over 1 and 0= and IF
	2 + swap  $10 and IF  negate  THEN
	vp-scrolling
	fdrop fdrop
	EXIT  THEN
    [: caller-w >o  >txy  tx
	[: act >o [ box-actor ] defers clicked o> ;] vp-needed|
	txy> o> ;] 1-?inside ;
vp-actor :method dnddrop ( rx ry addr u -- )
    [: caller-w >o  >txy  tx
	[: act >o [ box-actor ] defers dnddrop o> ;] vp-needed|
	txy> o> ;] 1-?inside ;
vp-actor :method dndmove ( rx ry -- )
    [: caller-w >o  >txy  tx
	[: act >o [ box-actor ] defers dndmove o> ;] vp-needed|
	txy> o> ;] 1-?inside ;
vp-actor :method scrolled ( axis dir rx ry -- )
    swap IF \ horizontal
	drop
    ELSE \ vertical
	vp-scrolling
    THEN  fdrop fdrop ;
vp-actor :method touchdown ( $rxy*n bmask -- )
    [: caller-w >o  >txy  >r tx$ r>
	[: act >o [ box-actor ] defers touchdown o> ;] vp-needed|
	txy> o> ;] 1-?inside ;
vp-actor :method touchup ( $rxy*n bmask -- )
    [: caller-w >o  >txy  >r tx$ r>
	[: act >o [ box-actor ] defers touchup o> ;] vp-needed|
	txy> o> ;] 1-?inside ;
vp-actor :method touchmove ( $rxy*n bmask -- )
    dup 2 or 2 = grab-move? o = and IF
	drop xy@ vpxy!
    ELSE
	[: caller-w >o  >txy  >r tx$ r>
	    [: act >o [ box-actor ] defers touchmove o> ;] vp-needed|
	    txy> o> ;] 1-?inside
    THEN ;
vp-actor :method ekeyed ( ekey -- )
    caller-w >o
    [: act >o [ box-actor ] defers ekeyed o> ;] vp-needed| o> ;
vp-actor :method ukeyed ( ekey -- )
    caller-w >o
    [: act >o [ box-actor ] defers ukeyed o> ;] vp-needed| o> ;

: vp[] ( o -- o )
    >o vp-actor new !act o o> ;

\ slider actor

simple-actor class
    value: slide-vp
    fvalue: slider-sxy
end-class hslider-actor

hslider-actor class
end-class vslider-actor

: >hslide ( x -- )
    slider-sxy f- caller-w >o parent-w .w w f- +sync o> f/
    slide-vp >o vp-w w f- fdup { f: hmax } f*
    0e fmax hmax fmin fround to vp-x
    ?vpt-x IF  ['] +sync vp-needed  THEN o>
    caller-w .parent-w >o !size xywhd !resize o> ;

hslider-actor :method touchmove ( $rxy*n bmask -- ) 
    grab-move? IF
	slide-vp .act anim-del
	drop xy@ fdrop >hslide
    ELSE
	2drop
    THEN ;
hslider-actor :method clicked ( x y b n -- )
    click( o h. caller-w h. ." slider click " fover f. fdup f. over . dup . cr )
    slide-vp .act anim-del
    1 and IF
	drop fdrop caller-w .parent-w .childs[] $@ drop @ .w f- to slider-sxy
	o >grab-move?
    ELSE
	drop fdrop >hslide
	false to grab-move?
    THEN ;

: (hslider[]) ( vp o class -- )
    swap >o o swap new to act
    act >o to caller-w to slide-vp -1e to slider-sxy
    caller-w slide-vp >o to vp-hslider o> o> o> ;

: hslider[] ( vp o -- ) hslider-actor (hslider[]) ;

hslider-actor class
end-class hsliderleft-actor
hslider-actor class
end-class hsliderright-actor

hsliderleft-actor :method clicked ( rx ry b n -- )
    fdrop fdrop  1 and 0= swap 1 u<= and IF
	slide-vp >o w         act o> ?dup-IF  >o  o anim-del
	set-startxy to vmotion-dx  0e to vmotion-dt  0e to vmotion-dy
	0.333e o ['] vp-scroll >animate o>  ELSE fdrop THEN
    THEN ;
hsliderright-actor :method clicked ( rx ry b n -- )
    fdrop fdrop  1 and 0= swap 1 u<= and IF
	slide-vp >o w fnegate act o> ?dup-IF  >o  o anim-del
	set-startxy to vmotion-dx  0e to vmotion-dt  0e to vmotion-dy
	0.333e o ['] vp-scroll >animate o>  ELSE fdrop THEN
    THEN ;

: hsliderleft[] ( vp o -- ) hsliderleft-actor (hslider[]) ;
: hsliderright[] ( vp o -- ) hsliderright-actor (hslider[]) ;

: >vslide ( x -- )
    slider-sxy fswap f- caller-w >o parent-w .h h f- +sync o> f/
    slide-vp >o vp-h h d f+ f- fdup { f: vmax } f*
    0e fmax vmax fmin fround to vp-y
    ?vpt-y IF  ['] +sync vp-needed  THEN  o>
    caller-w .parent-w >o !size xywhd !resize o> ;

vslider-actor :method touchmove ( $rxy*n bmask -- )
    event( o h. caller-w h. ." slider move " 2dup .touch )
    grab-move? IF
	slide-vp .act anim-del
	drop xy@ fnip >vslide
    ELSE  2drop  THEN ;
vslider-actor :method clicked ( x y b n -- )
    click( o h. caller-w h. ." slider click " fover f. fdup f. over . dup . cr )
    slide-vp .act anim-del
    1 and IF
	drop fnip caller-w .parent-w .childs[] $@ cell- + @ .h f+
	to slider-sxy
	o >grab-move?
    ELSE
	drop fnip >vslide
	false to grab-move?
    THEN ;

: (vslider[]) ( vp o class -- )
    swap >o o swap new to act
    act >o to caller-w to slide-vp -1e to slider-sxy
    caller-w slide-vp >o to vp-vslider o> o> o> ;
: vslider[] ( vp o -- ) vslider-actor (vslider[]) ;

vslider-actor class
end-class vsliderup-actor
vslider-actor class
end-class vsliderdown-actor

vsliderup-actor :method clicked ( rx ry b n -- )
    fdrop fdrop  1 and 0= swap 1 u<= and IF
	slide-vp >o h         act o> ?dup-IF  >o  o anim-del
	set-startxy to vmotion-dy  0e to vmotion-dt  0e to vmotion-dx
	0.333e o ['] vp-scroll >animate o>  ELSE  fdrop  THEN
    THEN ;
vsliderdown-actor :method clicked ( rx ry b n -- )
    fdrop fdrop  1 and 0= swap 1 u<= and IF
	slide-vp >o h fnegate act o> ?dup-IF  >o  o anim-del
	set-startxy to vmotion-dy  0e to vmotion-dt  0e to vmotion-dx
	0.333e o ['] vp-scroll >animate o>  ELSE  fdrop  THEN
    THEN ;

: vsliderup[] ( vp o -- ) vsliderup-actor (vslider[]) ;
: vsliderdown[] ( vp o -- ) vsliderdown-actor (vslider[]) ;

\ edit widget

: edit$!len ( len -- )
    \ precaution for password edit
    edit$ @ $@len over = IF  drop  EXIT  THEN
    0 { w^ new$ } new$ $!len
    edit$ @ $@ new$ $@ rot umin move
    edit$ @ $@ erase edit$ @ $free
    new$ @ edit$ @ ! ;

: grow-edit$ { max span addr pos1 more -- max span addr pos1 true }
    max span more + u> IF  max span addr pos1 true  EXIT  THEN
    span more + edit$!len
    edit$ @ $@ swap span swap pos1 true ;

: eins-string ( max span addr pos addr1 u1 -- max span' addr pos' )
    2>r r@ grow-tib 0= IF  edit-error 2rdrop  EXIT  THEN
    >edit-rest 2r@ 2swap r@ + insert
    r@ + rot r> + -rot  rdrop ;

edit-widget edit-out !

bl cells buffer: edit-ctrlkeys
xchar-ctrlkeys edit-ctrlkeys bl cells move
keycode-limit keycode-start - cells buffer: edit-ekeys
std-ekeys edit-ekeys keycode-limit keycode-start - cells move

' kill-prefix   is everychar
' edit-ctrlkeys is ctrlkeys
' edit-ekeys    is ekeys
' grow-edit$    is grow-tib
' eins-string   is insert-string

0 Value xselw
0 Value outselw

also
[IFDEF] android
    jni
    : android-seteditline ( span addr pos -- span addr pos )
	2dup xcs swap >r >r
	2dup swap make-jstring r> xselw clazz .setEditLine r>
	+sync ;
    ' android-seteditline is edit-update
[ELSE]
    ' noop is edit-update \ no need to do that here
[THEN]
' noop is edit-error  \ no need to make annoying bells
' clipboard!     is paste!
[IFUNDEF] primary!     ' clipboard! alias primary! [THEN]
[IFUNDEF] primary@     ' clipboard@ alias primary@ [THEN]
' clipboard@     is paste@
previous

\ extra key bindings for editors

simple-actor class
    defer: edit-next-line
    defer: edit-prev-line
    value: edit-w
    addressable: $value: prev-text$
    defer: edit-enter
    defer: edit-filter
    defer: edit-engaged
end-class edit-actor

also [IFDEF] jni  jni [THEN]
: edit-copy ( max span addr pos1 -- max span addr pos1 false )
    >r 2dup swap r@ safe/string xselw min clipboard!
    r> 0 ;
previous
: edit-cut ( max span addr pos1 -- max span addr pos1 false )
    edit-copy drop >r
    2dup swap r@ safe/string xselw delete
    swap xselw - swap
    r> edit-update 0 ;
: edit-bs ( max span addr pos1 -- max span addr pos1 false )
    xselw 0> IF  edit-cut  ELSE  ?xdel  THEN ;
: edit-del ( max span addr pos1 -- max span addr pos1 false )
    xselw 0> IF  edit-cut  ELSE  <xdel>  THEN ;

Defer anim-ins

: edit-ins$ ( max span addr pos1 addr u -- max span' addr pos1' )
    anim-ins
    xselw 0> IF  $make { w^ str } edit-cut drop str $@ insert-string
	str $free
    ELSE  insert-string  THEN ;

: edit-split-ins$ ( max span addr pos1 addr u -- max span' addr pos1' )
    BEGIN  #lf $split 2over + >r over r> u>  WHILE
	    2>r  edit-ins$ edit-update edit-enter drop 2drop 2drop
	    0 edit$!len edit$ @ $@ swap 0 tuck  2r>
    REPEAT  2drop edit-ins$ edit-update ;

[IFDEF] android also android also jni [THEN]
: setstring> ( max span addr pos1 - max span addr pos2 )
    setstring$ $@len 0> xselw 0> and  IF
	edit-cut drop  setstring$ $@ xins-string  setstring$ $free
    THEN  edit-update
    [IFDEF] restartkb restartkb [THEN] ;

: edit-paste ( max span addr pos1 - max span addr pos2 false )
    setstring> clipboard@ edit-split-ins$ edit-update 0 ;

: xedit-enter ( max span addr pos1 -- max span addr pos2 true )
    setstring> edit-enter ;
: edit-selall ( max span addr pos1 -- max span addr pos2 false )
    drop over to outselw 0 xretype ;

: edit-insert ( max span addr pos1 -- max span addr pos2 false )
    case vt100-modifier @
	1 of  edit-paste  endof
	4 of  >r 2dup swap r@ safe/string xselw min clipboard! r> 0   endof
	5 of  setstring> primary@ edit-split-ins$ edit-update 0  endof
    false swap
    endcase ;
[IFDEF] android previous previous [THEN]

: xedit-repaint ( -- flag ) xselw to outselw false ;

' edit-next-line ctrl N bindkey
' edit-prev-line ctrl P bindkey
' edit-paste     ctrl V bindkey
' edit-paste     ctrl Y bindkey
' edit-copy      ctrl C bindkey
' edit-cut       ctrl X bindkey
' edit-cut       ctrl W bindkey
' xedit-enter    #lf    bindkey
' xedit-enter    #cr    bindkey
' xedit-repaint  ctrl L bindkey
' edit-bs        ctrl H bindkey
' edit-del       ctrl D bindkey
' (xtab-expand)  #tab   bindkey

' edit-next-line k-down   ebindkey
' edit-prev-line k-up     ebindkey
' edit-next-line k-voldown ebindkey
' edit-prev-line k-volup  ebindkey
' edit-next-line k-next   ebindkey
' edit-prev-line k-prior  ebindkey
' edit-enter     k-eof    ebindkey
' xedit-enter    k-enter  ebindkey
' xedit-repaint  k-winch  ebindkey
' edit-bs        k-backspace ebindkey
' edit-del       k-delete ebindkey
' (xtab-expand)  k-tab    ebindkey
' edit-selall    k-sel    ebindkey
' edit-insert    k-insert ebindkey
\ ' edit-copy      'W' k-alt-mask or ebindkey

edit-terminal edit-out !

: *xins        anim-ins  defers insert-char ;

' *xins        is insert-char

\ edit things

: edit-xt ( ... xt o:actor -- )
    \ pass xt to the editor
    \ xt has ( ... addr u curpos cursize -- addr u curpos cursize ) as stack effect
    *ins-o off
    history >r  >r  0 to history
    edit-w .text$ to prev-text$ \ backup of previous text
    edit-w >o
    addr text$ curpos over $@len umin cursize 0 max o> to xselw
    >r dup edit$ ! $@ swap over swap  0 to outselw r>
    r> catch
    >r edit-w >o to curpos  outselw to cursize o> drop
    edit$!len drop  edit-filter
    r>  r> to history  +sync +resize  throw ;

: edit>curpos ( x o:actor -- )
    edit-w >o  text-font to font  w text-w text-scale! 
    x f- border f- w border f2* f- text-w f/ f/
    text$ pos-string to curpos  prefix-off
    o>  +sync ;

[IFDEF] sync+config
    :is sync+config +sync +resize ;
[THEN]

[IFUNDEF] -scan
    : -scan ( addr u char -- addr' u' )
	>r  BEGIN  dup  WHILE  1- 2dup + c@ r@ =  UNTIL  THEN
	rdrop ;
[THEN]
: select-word ( o:edit-w -- )
    start-curpos 0< IF  curpos to start-curpos  THEN
    text$ start-curpos start-cursize + curpos cursize + umax safe/string
    bl scan drop { end }
    text$ drop start-curpos curpos umin bl -scan + dup c@ bl = - { start }
    start text$ drop - to curpos
    end start - to cursize +sync ;
: select-line ( o:edit-w -- )
    0 to curpos text$ nip to cursize +sync ;
: sel>primary ( o:edit-w -- )
    text$ curpos safe/string cursize min 0 max primary! ;
: pos>select ( -- )
    curpos start-curpos 2dup - abs to cursize umin to curpos +sync ;
: end-selection ( o:edit-w -- )
    start-curpos 0>= IF
	pos>select
	case select-mode
	    1 of select-word endof
	    2 of select-line endof
	endcase
	-1 to start-curpos
    THEN ;
: start-selection ( fx fy b n -- )
    *ins-o off
    edit-w .start-curpos 0< IF
	1- 2/ to select-mode
	drop fdrop edit>curpos  edit-w >o
	0 to cursize +sync
	curpos  to start-curpos
	cursize to start-cursize
	case select-mode
	    1 of select-word endof
	    2 of select-line endof
	endcase
	curpos  to start-curpos
	cursize to start-cursize
	o>
	o >grab-move?
    ELSE
	false to grab-move?
	2drop fdrop fdrop
    THEN ;
: expand-selection ( $xy -- )
    edit-w .start-curpos 0>= IF
	['] setstring> edit-xt
	xy@ fdrop edit>curpos
	edit-w >o pos>select
	case  select-mode
	    1 of select-word endof
	    2 of select-line endof
	endcase
	o>
    ELSE  drop
    THEN ;

[IFDEF] cursor-type
    edit-actor :method entered ( -- ) 9 to cursor-type [ edit-actor action-of entered compile, ] ;
    edit-actor :method left ( -- ) 1 to cursor-type [ edit-actor action-of left compile, ] ;
[THEN]
edit-actor :method ekeyed ( key o:actor -- )
    [: 4 roll dup keycode-start and 0= k-ctrl-mask and invert and
	everychar >control edit-control drop edit-update +sync +resize ;] edit-xt ;
edit-actor :method ukeyed ( addr u o:actor -- )
    dup 1 = vt100-modifier @ 8 = and IF
	drop c@ 2 mask-shift# lshift or ekeyed
    ELSE
	[: 2rot prefix-off edit-ins$ edit-update +sync +resize ;] edit-xt
    THEN ;
edit-actor :method defocus ( o:actor -- )
    ['] setstring> edit-xt edit-w >o -1 to cursize o> +sync ;
edit-actor :method focus ( o:actor -- )
    edit-w >o  0 to cursize o> +sync +keyboard  edit-engaged ;
edit-actor :method touchmove ( $rxy*n bmask -- )
    case
	dup 1 u<= ?of drop  expand-selection  endof
	nip
    endcase +sync +resize ;
edit-actor :method clicked ( o:actor rx ry b n -- )
    click( o h. caller-w h. ." edit click " fover f. fdup f. over . dup . cr )
    o engage
    ['] setstring> edit-xt
    dup 1 and IF  start-selection
    ELSE
	false to grab-move?
	swap
	case
	    dup 1 u<= ?of drop
		2 - 6 mod 2 +
		{ clicks } fdrop edit>curpos
		edit-w >o
		case clicks
		    2 of  end-selection  endof
		    4 of  select-word    endof
		    6 of  select-line    endof
		endcase
		[IFDEF] primary$
		    primary$ $@len cursize 0<> or IF  sel>primary  THEN
		[THEN]
		-1 to start-curpos
		0  to start-cursize
		o>
	    endof
	    2 of  2 = IF
		    ['] setstring> edit-xt
		    fdrop edit>curpos
		    [: primary@ edit-split-ins$ ;] edit-xt
		ELSE  fdrop fdrop  THEN  endof
	    4 of  ( menu   )  drop fdrop fdrop  endof
	    nip fdrop fdrop
	endcase
    THEN  +sync +resize ;
[IFDEF] dnd@
    edit-actor :method dnddrop ( o:actor rx ry addr u -- )
	dnd( [: cr ." Editor dnd drop '" dnd@ type ." '" ;] do-debug )
	['] setstring> edit-xt
	fdrop edit>curpos
	[: dnd@
	    [: BEGIN #lf $split dup WHILE 2swap type space
		REPEAT 2drop type ;] $tmp edit-ins$ ;] edit-xt ;
    edit-actor :method dndmove ( o:actor rx ry -- )
	dnd( [: cr ." Editor dnd move '" fover f. fdup f. ;] do-debug )
	fdrop fdrop ;
[THEN]
: edit[] ( o widget xt -- o ) { xt }
    swap >o edit-actor new to act
    o act >o to caller-w to edit-w
    xt        is edit-enter
    ['] false is edit-next-line
    ['] false is edit-prev-line
    ['] noop  is edit-filter
    ['] noop  is edit-engaged o>
    o o> ;

: filter[] ( o xt -- o ) { xt }
    >o act >o xt is edit-filter o> o o> ;
: engaged[] ( o xt -- o ) { xt }
    >o act >o xt is edit-engaged o> o o> ;
