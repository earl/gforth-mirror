\ Presentation of Wayland integration in ΜΙΝΩΣ2

\ Author: Bernd Paysan
\ Copyright (C) 2025 Bernd Paysan

\ This program is free software: you can redistribute it and/or modify
\ it under the terms of the GNU General Public License as published by
\ the Free Software Foundation, either version 3 of the License, or
\ (at your option) any later version.

\ This program is distributed in the hope that it will be useful,
\ but WITHOUT ANY WARRANTY; without even the implied warranty of
\ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
\ GNU Affero General Public License for more details.

\ You should have received a copy of the GNU General Public License
\ along with this program.  If not, see <http://www.gnu.org/licenses/>.

require minos2/widgets.fs

[IFDEF] android
    also jni hidekb also android >changed hidestatus >changed previous previous
[THEN]

also minos

ctx 0= [IF]  window-init  [THEN]

require minos2/font-style.fs
require minos2/text-style.fs
require minos2/presentation-support.fs
require unix/open-url.fs

:noname 44e update-size# ; is rescaler
rescaler

m2c:animtime% f@ 3e f* m2c:animtime% f!

tex: gforth-logo
' gforth-logo "net2o-minos2.png" 0.5e }}image-file Constant gforth-logo-glue drop

: ``` ( -- )
    BEGIN  refill  WHILE  source "```" str= 0= WHILE
		source }}text /left  REPEAT  THEN
    source nip >in ! ;

: logo-img ( xt xt -- o o-img ) 2>r
    baseline# 0e to baseline#
    {{ 2r> }}image-tex dup >r /right
    glue*l }}glue
    }}v >o font-size# f2/ to border o o>
    to baseline# r> ;

' }}i18n-text is }}text'

{{
{{ glue-left @ }}glue

\ page 0
{{
    $000000FF $FFFFFFFF pres-frame
    {{
	glue*l }}glue \ ) $CCDDDD3F color, 4e }}frame dup .button1
	l" Wayland in ΜΙΝΩΣ2" /title
	l" Notation Matters" /subtitle
	{{
	    {{
		glue*l }}glue
		tex: burladingen-wappen
		' burladingen-wappen "Wappen_Burladingen.svg.png" 0.5e }}image-file
		Constant wappen-burladingen-glue /center
		glue*l }}glue
	    }}v
	    glue*2 }}glue
	}}z
	l" Bernd Paysan" /author
	l" Forth–Tagung 2025 in Burladingen" /location
	glue*l }}glue \ ) $CCDDDD3F color, 4e }}frame dup .button1
    }}v box[] >o font-size# to border o Value title-page o o>
}}z box[] dup >slides

\ page 6
{{
    $440044FF $FFBFFFFF pres-frame
    {{
	l" Was ist Wayland?" /title
	l" Ein objektbasiertes RPC zur Kommunikation mit dem Compositor" /subsection
	vt{{
	    l" Protokolle " l" Klassen mit Methoden auf beiden Seiten" b\\
	    l" XML–Dateien " l" Beschreiben die Protokolle" b\\
	    l" Callbacks " l" Für die Implementierung der eigenen Seite" b\\
	    l" Listener " l" Objekt–Instanzen auf der eigenen Seite (mit mehreren Callbacks)" b\\
	    l" Registry " l" Protokoll, welche Protokolle der Compositor implementiert" b\\
	    glue*l }}glue \ ) $CCDDDD3F 4e }}frame dup .button1
	}}vt
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 6
{{
    $440044FF $FFBFFFFF pres-frame
    {{
	l" Was kann Wayland?" /title
	l" Inzwischen brauchbarer Ersatz für X11" /subsection
	vt{{
	    l" Breaks Everything " l" War mal am Anfang, ist inzwischen vorbei" b\\
	    l" Fractional Scaling " l" Auf aktuellen Displays sehr sinnvoll" b\\
	    l" Clipboard " l" und Primary Selection gehen (aber blöd implementiert)" b\\
	    l" Farbmanagement " l" Ist noch eine Baustelle" b\\
	    l" OpenGLES " l" Ging von Anfang an gut" b\\
	    glue*l }}glue \ ) $CCDDDD3F 4e }}frame dup .button1
	}}vt
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 6
{{
    $444400FF $FFFFBFFF pres-frame
    {{
	l" Ein Protokoll implementieren" /title
	l" Am Beispiel xdg_wm_base" /subsection
	\skip \mono
```
<cb xdg_wm_base
:cb ping ( data xdg_wm_base serial -- )
    serial( dup [: cr ." pong serial: " h. ;] do-debug )
    dup to last-serial
    xdg_wm_base_pong drop ;
cb>
```
	\sans
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 6
{{
    $444400FF $FFFFBFFF pres-frame
    {{
	l" Ein Protokoll implementieren" /title
	l" Wie sähe das ohne diese Makros aus?" /subsection
	\skip \mono
```
:noname ( data xdg_wm_base serial -- )
    serial( dup [: cr ." pong serial: " h. ;] do-debug )
    dup to last-serial
    xdg_wm_base_pong drop ; xdg_wm_base_listener-ping:
Create xdg-wm-base-listener ,
```
	\sans
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 6
{{
    $004400FF $BFFFBFFF pres-frame
    {{
	l" Komplexeres Protokoll" /title
	l" Am Beispiel wl_data_source" /subsection
	\skip \mono \small
```
<cb wl_data_source
:cb action { data source dnd-action -- }
    wayland( dnd-action [: cr ." ds action: " h. ;] do-debug ) ;
:cb dnd_finished { data source -- } ;
:cb dnd_drop_performed { data source -- } ;
:cb cancelled { data source -- }
    wayland( [: cr ." ds cancelled" ;] do-debug )
    0 to data-source  0 to my-clipboard
    source wl_data_source_destroy ;
:cb send { data source d: mime-type fd -- }
    wayland( mime-type data [: cr ." send " id. ." type " type ;] do-debug )
    data fd clipout$ .set-out ;
:cb target { data source d: mime-type -- }
    wayland( data mime-type [: cr ." ds target: " type space id. ;] do-debug ) ;
cb>
```
	\sans \normal
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 6
{{
    $004444FF $BFFFFFFF pres-frame
    {{
	l" Trigger–Values" /title
	l" Values, die bei Zuweisungen Code ausführen" /subsection
	\skip \mono \small
```
0 ' noop trigger-Value wl-compositor \ wayland compositor
0 ' noop trigger-Value wl-surface    \ wayland surface
0 ' noop trigger-Value wl-shell      \ wayland shell

wl-registry set-current

5 wl: wl_compositor
:trigger-on( wl-compositor )
    wl-compositor wl_compositor_create_surface to wl-surface
    wl-compositor wl_compositor_create_surface to cursor-surface ;
1 wl: wl_shell
:trigger-on( wl-shell wl-surface )
    wl-shell wl-surface wl_shell_get_shell_surface to shell-surface ;
```
	\sans \normal
    }}v box[] >bdr
}}z box[] /flip dup >slides

\ page 17
{{
    $000000FF $FFFFFFFF pres-frame
    {{
	l" Literatur & Links" /title
	vt{{
	    l" Gforth Team " l" Gforth Homepage" bi\\
	    l" 🔗" l" https://gforth.org/" bm\\
	    [: "https://gforth.org/" open-url ;] 0 click[]
	}}vt
	glue*l }}glue
    }}v box[] >bdr
}}z box[] /flip dup >slides

' }}text is }}text'

\ end
glue-right @ }}glue
}}h box[]
{{
    ' gforth-logo     gforth-logo-glue logo-img solid-frame
}}z
}}z slide[]
to top-widget

also opengl

[IFDEF] writeout-en
    lsids ' .lsids s" ef2018/en" r/w create-file throw
    dup >r outfile-execute r> close-file throw
[THEN]

previous

script? [IF]
    next-arg s" time" str= [IF]  +db time( \ ) [THEN]
    presentation bye
[ELSE]
    presentation
[THEN]

\\\
Local Variables:
forth-local-words:
    (
     ("[a-z0-9]+(" immediate (font-lock-comment-face . 1)
      ")" nil comment (font-lock-comment-face . 1))
     (("x\"" "l\"") immediate (font-lock-string-face . 1)
      "[\"\n]" nil string (font-lock-string-face . 1))
    )
forth-local-indent-words:
    (
     (("{{" "vt{{") (0 . 2) (0 . 2) immediate)
     (("}}h" "}}v" "}}z" "}}vp" "}}p" "}}vt") (-2 . 0) (-2 . 0) immediate)
    )
End:
