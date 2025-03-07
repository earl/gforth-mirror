#! /usr/local/bin/gforth

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2000,2002,2003,2004,2006,2007,2008,2013,2015,2016,2017,2019,2020,2021,2022,2024 Free Software Foundation, Inc.

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

\ This relies on inetd/xinetd/systemd:

\ To run the server on port 4444, do the following:

\ Add the following line to /etc/services:
\ ==========================================================
\ gforth          4444/tcp
\ ==========================================================

\ If the user wwwrun hasn't been created yet, create it now (as root):
\ useradd -r -g nogroup -d /var/www -s /usr/sbin/nologin wwwrun

\ === inetd ===

\ If you use inetd, add the following line to /etc/inetd.conf:
\ ==========================================================
\ gforth stream tcp nowait.10000   wwwrun   /usr/share/gforth/<version>/httpd.fs
\ ==========================================================
\ and make sure httpd.fs is made executable

\ If you want port 80, replace the service "gforth" with "http"

\ === xinetd ===

\ If you use xinetd, create the following service as
\ /etc/xinetd.d/gforth:
\ ==========================================================
\ service gforth
\ {
\         socket_type     = stream
\         protocol        = tcp
\         wait            = no
\         user            = wwwrun
\         server          = /usr/bin/gforth
\         server_args     = httpd.fs
\ }
\ ==========================================================

\ If you want port 80, replace the service "gforth" with "http"

\ === systemd ===

\ If you use systemd, create the following socket as
\ /usr/lib/systemd/system/gforth-httpd.socket:
\ ==========================================================
\ [Unit]
\ Description=Gforth httpd socket
\ 
\ [Socket]
\ ListenStream=4444
\ Accept=yes
\ 
\ [Install]
\ WantedBy=sockets.target
\ ==========================================================
\ And create the following service as
\ /usr/lib/systemd/system/gforth-httpd@.service:
\ ==========================================================
\ [Unit]
\ Description=Gforth httpd server
\ 
\ [Service]
\ ExecStart=-/usr/bin/gforth --die-on-signal httpd.fs
\ User=wwwrun
\ StandardInput=socket
\ ==========================================================
\ enable with: systemctl enable gforth-httpd.socket
\ start with: systemctl start gforth-httpd.socket
\ check with: systemctl status gforth-httpd.socket

\ If you want port 80, replace port 4444 with 80

warnings off

Variable DocumentRoot  s" /var/www/html/" DocumentRoot $!
Variable UserDir       s" public_html/"   UserDir      $!

Variable url
Variable posted
Variable url-args
Variable protocol
Variable data
Variable active
Variable command?

: get ( addr -- )  name rot $! ;
: get-rest ( addr -- )  source >in @ /string dup >in +! rot $! ;
: get-rest[] ( addr -- )  source >in @ /string dup >in +! rot $+[]! ;

wordlist constant values
wordlist constant commands

: value-def ( "name" -- )
    get-current >r definitions
    name 2dup 1- nextname Variable
    r> set-current nextname here cell - Create , ;

: value:  ( "name" -- )
    value-def DOES> @ get-rest ;
: >values  values 1 set-order command? off ;

\ HTTP protocol commands                               26mar00py

: rework-% ( add -- )
    { url }  base @ >r hex
    0 url $@len 0 ?DO
	url $@ drop I + c@ dup '%' = IF
	    drop 0. url $@ I 1+ /string
	    2 min dup >r >number r> swap - >r 2drop
	ELSE  0 >r  THEN  over url $@ drop + c!  1+
    r> 1+ +LOOP  url $!len
    r> base ! ;

: rework-? ( addr -- )
    dup >r $@ '?' $split url-args $! nip r> $!len ;

: get-url ( -- )
    url get protocol get-rest
    url rework-? url rework-% >values ;

commands set-current

: GET   get-url data on  active off ;
: POST  get-url data on  active on  ;
: HEAD  get-url data off active off ;

\ HTTP protocol values                                 26mar00py

values set-current

value: User-Agent:
value: Pragma:
value: Host:
value: Accept:
value: Accept-Encoding:
value: Accept-Language:
value: Accept-Charset:
value: Via:
value: X-Forwarded-For:
value: Cache-Control:
value: Connection:
value: Referer:
value: Content-Type:
value: Content-Length:
value: Keep-Alive:
value: Cookie:

definitions

Variable maxnum

: ?cr ( -- )
  #tib @ 1 >= IF  source 1- + c@ #cr = #tib +!  THEN ;
: refill-loop ( -- flag )
  base @ >r base off
  BEGIN  refill ?cr  WHILE  ['] interpret bt-rp0-catch drop  >in @ 0=  UNTIL
  true  ELSE  maxnum off false  THEN  r> base ! ;
: get-input ( -- flag ior )
  s" /nosuchfile" url $!  s" HTTP/1.0" protocol $!
  s" close" connection $!
  infile-id push-file loadfile !  loadline off  blk off
  get-order n>r get-recognizers n>r
  commands 1 set-order  ['] rec-nt 1 set-recognizers
  command? on  ['] refill-loop catch
  Keep-Alive $@ snumber? dup 0> IF  nip  THEN  IF  maxnum !  THEN
  active @ IF  s" " posted $! Content-Length $@ snumber? drop
      posted $!len  posted $@ infile-id read-file throw drop
  THEN  nr> set-recognizers nr> set-order  pop-file ;

\ Rework HTML directory                                26mar00py

Variable htmldir

: rework-htmldir ( addr u -- addr' u' / ior )
  htmldir $! htmldir $@ compact-filename htmldir $!len drop
  htmldir $@ s" ../" string-prefix?
  IF    -1 EXIT  THEN  \ can't access below current directory
  htmldir $@ s" ~" string-prefix?
  IF    UserDir $@ htmldir dup $@ 2dup '/' scan '/' skip
        nip - nip $ins
  ELSE  DocumentRoot $@ htmldir 0 $ins  THEN
  htmldir $@ 1- 0 max + c@ '/' = htmldir $@len 0= or
  IF  s" index.html" htmldir dup $@len $ins  THEN
  htmldir $@ file-status nip ?dup ?EXIT
  htmldir $@ ;

\ MIME type handling                                   26mar00py

: >mime ( addr u -- mime u' )
  2dup tuck over + 1- ?DO
  I c@ '.' = ?LEAVE  1-  -1 +LOOP  /string ;

: >file ( addr u -- size fd )
  r/o bin open-file throw dup
  >r file-size throw drop
  ." Accept-Ranges: bytes" cr
  ." Content-Length: " dup 0 .r cr r> ;
: transparent ( size fd -- )
    { fd } $4000 allocate throw swap dup 0 ?DO
	2dup over swap $4000 min fd read-file throw type
	$4000 - $4000 +LOOP  drop
    free fd close-file throw throw ;

\ Keep-Alive handling                                  26mar00py

: .connection ( -- )
  ." Connection: "
  connection $@ s" Keep-Alive" str= maxnum @ 0> and
  IF  connection $@ type cr
      ." Keep-Alive: timeout=15, max=" maxnum @ 0 .r cr
      -1 maxnum +!  ELSE  ." close" cr maxnum off  THEN ;

: transparent: ( addr u -- )
  Create  here over 1+ allot place
  DOES>  >r  >file
  .connection
  ." Content-Type: "  r> count type cr cr
  data @ IF  transparent  ELSE  nip close-file throw  THEN ;

\ mime types                                           26mar00py

: mime-read ( addr u -- )
    r/o open-file throw
    push-file loadfile !  0 loadline ! blk off
    BEGIN  refill  WHILE
	    parse-name over c@ '#' <> over 0<> and  IF
		BEGIN  parse-name dup  WHILE
			nextname 2dup transparent:  REPEAT
		2drop
	    THEN  2drop
    REPEAT  loadfile @ close-file pop-file throw ;

: lastrequest
  ." Connection: close" cr maxnum off
  ." Content-Type: text/html" cr cr ;

wordlist constant mime
mime set-current

s" application/pgp-signature" transparent: sig
s" application/x-bzip2" transparent: bz2
s" application/x-gzip" transparent: gz
s" /etc/mime.types" ' mime-read catch [IF]  2drop
    \ no /etc/mime.types found on this machine,
    \ generating the most important types:
    s" text/html" transparent: html
    s" image/gif" transparent: gif
    s" image/png" transparent: png
    s" image/jpg" transparent: jpg
[THEN]
: shtml ( addr u -- )
    lastrequest
    data @ IF  also forth included previous  ELSE  2drop  THEN ;

definitions

s" text/plain" transparent: txt

\ http errors                                          26mar00py

: .server ( -- )
    ." Server: Gforth httpd/1.0 ("
    s" os-class" environment? IF  type  THEN  ." )" cr ;
: .ok  ( -- ) ." HTTP/1.1 200 OK" cr .server ;
: html-error ( n addr u -- )
    ." HTTP/1.1 " third . 2dup type cr .server
    third &405 = IF ." Allow: GET, HEAD, POST" cr  THEN
    lastrequest
    ." <HTML><HEAD><TITLE>" third . 2dup type
    ." </TITLE></HEAD>" cr
    ." <BODY><H1>" type drop ." </H1>" cr ;
: .trailer ( -- )
    ." <HR><ADDRESS>Gforth httpd 1.0</ADDRESS>" cr
    ." </BODY></HTML>" cr ;
: .nok ( -- )
    command? @ IF  &405 s" Method Not Allowed"
    ELSE  &400 s" Bad Request"  THEN  html-error
    ." <P>Your browser sent a request that this server "
    ." could not understand.</P>" cr
    ." <P>Invalid request in: <CODE>"
    error-stack cell+ 2@ swap type
    ." </CODE></P>" cr .trailer ;
: .nofile ( -- )
    &404 s" Not Found" html-error
    ." <P>The requested URL <CODE>" url $@ type
    ." </CODE> was not found on this server</P>" cr .trailer ;

\ http server                                          26mar00py

Defer redirect?  ( addr u -- addr' u' t / f )
Defer redirect ( addr u -- )
:noname 2drop false ; IS redirect?

: http ( -- )
    get-input  IF  .nok  ELSE
    IF  url $@ 1 /string 2dup redirect? IF  redirect 2drop  ELSE
	rework-htmldir
	dup 0< IF  drop .nofile
	ELSE  .ok  2dup >mime mime search-wordlist
	    0= IF  ['] txt  THEN  catch IF  maxnum off THEN
	THEN  THEN  THEN  THEN  outfile-id flush-file throw ;

: httpd  ( n -- )
  dup maxnum ! 0 <# #S #> Keep-Alive $!
  maxnum @ 0 DO  ['] http catch  maxnum @ 0= or  ?LEAVE  LOOP ;

script? [IF]
    :noname &100 httpd stdout flush-file 0 (bye) ; is 'quit
    ' noop IS bootmessage
[THEN]

\ Use Forth as server-side script language             26mar00py

: $> ( -- )
    BEGIN  source >in @ /string s" <$" search  0= WHILE
        type cr refill  0= UNTIL  EXIT  THEN
    nip source >in @ /string rot - dup 2 + >in +! type ;
: <HTML> ( -- )  ." <HTML>" $> ;

\ provide transparent proxying

include ./proxy.fs
