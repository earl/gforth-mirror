\ completion of stuff that is not generated by SWIG (yet?)

\ Authors: Bernd Paysan, Anton Ertl
\ Copyright (C) 2016,2017,2018,2019,2021,2023,2024 Free Software Foundation, Inc.

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

: ?include-wayland ( "file" -- )
    parse-name 2dup [: ." wayland/" type ;] $tmp open-fpath-file 0=
    IF  2drop close-file throw
	[: ." #include <" type ." >" cr ;] c-source-file-execute
    ELSE
	." Skip " type cr  postpone [ELSE]
    THEN ;

c-value wl_display_interface &wl_display_interface -- a
c-value wl_registry_interface &wl_registry_interface -- a
c-value wl_callback_interface &wl_callback_interface -- a
c-value wl_compositor_interface &wl_compositor_interface -- a
c-value wl_shm_pool_interface &wl_shm_pool_interface -- a
c-value wl_shm_interface &wl_shm_interface -- a
c-value wl_buffer_interface &wl_buffer_interface -- a
c-value wl_data_offer_interface &wl_data_offer_interface -- a
c-value wl_data_source_interface &wl_data_source_interface -- a
c-value wl_data_device_interface &wl_data_device_interface -- a
c-value wl_data_device_manager_interface &wl_data_device_manager_interface -- a
c-value wl_shell_interface &wl_shell_interface -- a
c-value wl_shell_surface_interface &wl_shell_surface_interface -- a
c-value wl_surface_interface &wl_surface_interface -- a
c-value wl_seat_interface &wl_seat_interface -- a
c-value wl_pointer_interface &wl_pointer_interface -- a
c-value wl_keyboard_interface &wl_keyboard_interface -- a
c-value wl_touch_interface &wl_touch_interface -- a
c-value wl_output_interface &wl_output_interface -- a
c-value wl_region_interface &wl_region_interface -- a
c-value wl_subcompositor_interface &wl_subcompositor_interface -- a
c-value wl_subsurface_interface &wl_subsurface_interface -- a
?include-wayland text-input-unstable-v3.c
c-value zwp_text_input_v3_interface &zwp_text_input_v3_interface -- a
c-value zwp_text_input_manager_v3_interface &zwp_text_input_manager_v3_interface -- a
[THEN]
?include-wayland xdg-shell.c
c-value xdg_wm_base_interface &xdg_wm_base_interface -- a
c-value xdg_positioner_interface &xdg_positioner_interface -- a
c-value xdg_surface_interface &xdg_surface_interface -- a
c-value xdg_toplevel_interface &xdg_toplevel_interface -- a
c-value xdg_popup_interface &xdg_popup_interface -- a
[THEN]
?include-wayland xdg-decoration-unstable-v1.c
c-value zxdg_toplevel_decoration_v1_interface &zxdg_toplevel_decoration_v1_interface -- a
c-value zxdg_decoration_manager_v1_interface &zxdg_decoration_manager_v1_interface -- a
[THEN]
?include-wayland primary-selection-unstable-v1.c
c-value zwp_primary_selection_device_manager_v1_interface &zwp_primary_selection_device_manager_v1_interface -- a
c-value zwp_primary_selection_device_v1_interface &zwp_primary_selection_device_v1_interface -- a
c-value zwp_primary_selection_offer_v1_interface &zwp_primary_selection_offer_v1_interface -- a
c-value zwp_primary_selection_source_v1_interface &zwp_primary_selection_source_v1_interface -- a
[THEN]
?include-wayland tablet-unstable-v2.c
c-value zwp_tablet_manager_v2_interface &zwp_tablet_manager_v2_interface -- a
c-value zwp_tablet_seat_v2_interface &zwp_tablet_seat_v2_interface -- a
c-value zwp_tablet_tool_v2_interface &zwp_tablet_tool_v2_interface -- a
c-value zwp_tablet_v2_interface &zwp_tablet_v2_interface -- a
c-value zwp_tablet_pad_ring_v2_interface &zwp_tablet_pad_ring_v2_interface -- a
c-value zwp_tablet_pad_strip_v2_interface &zwp_tablet_pad_strip_v2_interface -- a
c-value zwp_tablet_pad_group_v2_interface &zwp_tablet_pad_group_v2_interface -- a
c-value zwp_tablet_pad_v2_interface &zwp_tablet_pad_v2_interface -- a
[THEN]
?include-wayland fractional-scale-v1.c
c-value wp_fractional_scale_manager_v1_interface &wp_fractional_scale_manager_v1_interface -- a
c-value wp_fractional_scale_v1_interface &wp_fractional_scale_v1_interface -- a
[THEN]
?include-wayland viewporter.c
c-value wp_viewporter_interface &wp_viewporter_interface -- a
c-value wp_viewport_interface &wp_viewport_interface -- a
[THEN]
?include-wayland xdg-output-unstable-v1.c
c-value zxdg_output_manager_v1_interface &zxdg_output_manager_v1_interface -- a
[THEN]
?include-wayland xdg-activation-v1.c
c-value xdg_activation_v1_interface &xdg_activation_v1_interface -- a
[THEN]
