\ ----------------------------------------------------------------------
\ @file : bootload.fs
\ ----------------------------------------------------------------------
\
\ Last change: KS 16.07.2023 15:03:12
\ Project : microCore
\ Language : gforth_0.6.2
\ Last check in : $Rev: 647 $ $Date:: 2021-02-18 #$
\ @copyright (c): Free Software Foundation
\ @original author: ks - Klaus Schleisiek
\
\ @license: This file is part of microForth.
\ microForth is free software for microCore that loads on top of Gforth;
\ you can redistribute it and/or modify it under the terms of the
\ GNU General Public License as published by the Free Software Foundation,
\ either version 3 of the License, or (at your option) any later version.
\ This program is distributed in the hope that it will be useful,
\ but WITHOUT ANY WARRANTY; without even the implied warranty of
\ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
\ GNU General Public License for more details.
\ You should have received a copy of the GNU General Public License
\ along with this program. If not, see http://www.gnu.org/licenses/.
\
\ @brief : MicroCore load screen for a program that is synthesized into
\ the core. It is executed immediately after the FPGA has been configured
\ and it checks a connected E2prom for a valid program memory image with CRC.
\
\ Version Author   Date       Changes
\   2300    ks   09-Feb-2021  initial version
\ ----------------------------------------------------------------------
Only Forth also definitions

[IFDEF] unpatch     unpatch    [ENDIF]
[IFDEF] close-port  close-port [ENDIF]
[IFDEF] microcore   microcore  [ENDIF]   Marker microcore

include extensions.fs           \ Some System word (re)definitions for a more sympathetic environment
include ../vhdl/architecture_pkg.vhd
include microcross.fs           \ the cross-compiler

Target new                      \ go into target compilation mode and initialize target compiler

3 code-origin
0 data-origin

include constants.fs

\ This will be compiled into an area of unused trap locations for compaction
: pack   ( c u -- u' )       8 shift swap $FF and or ;

\ Set the code pointer to after the #psr trap, which compiles a 'noop exit'
#psr trap-addr H 2 + Tcp ! T

Host: eeloads   ( -- )  #octetts 1- 0 ?DO  T ld   H LOOP ;
Host: eepacks   ( -- )  #octetts 1- 0 ?DO  T pack H LOOP ;

: ee_set ( addr1 -- addr2 )  $20000 or EE-addr st ;

: eec@   ( addr -- 8b )      ee_set 2 - @ ;

: ee@    ( addr -- n )       ee_set 1- eeloads 1- @ eepacks ;

\ -----------------------------------------------------------------------
\ CRC16-CCITT
\ Width = 16 bits
\ Truncated polynomial = 0x1021
\ Initial value = 0xFFFF
\ -----------------------------------------------------------------------

$1021 Constant #polynom
$FFFF Constant #crc-init
$8000 Constant #X15-bit
$FFFF Constant #crc-mask
    2 Constant #bytes/crc

: crc-step  ( w crc1 -- w' crc2 ) \ process one bit
   2dup xor >r   2* swap 2* swap
   r> #X15-bit and IF  #polynom xor  THEN
;
: crc8  ( crc1 c -- crc2 )  \ process one byte
   8 shift swap &7 FOR  crc-step  NEXT  nip
;
: crc-check ( addr length -- crc )  \ process string of bytes
   #crc-init -rot ?FOR  dup >r   eec@ crc8   r> 1+  NEXT  drop
   #crc-mask and
;
: bootcode?  ( -- f )  #boot-mem
   BEGIN  dup ee@ over ee@ = UNTIL   \ wait for E2prom to initialize
   dup ee@ ?dup IF  swap #octetts + swap #bytes/crc + crc-check  THEN  0=
;
: delay      ( -- )   &800 time + BEGIN  dup time? UNTIL drop ;

: boot  ( -- )
   0 dup   1 pST 1+ pST >r    \ initialize with 0 for later check
   bootcode? IF r@ 2 -   #boot-mem   dup #octetts +   swap ee@ 1-
                FOR  dup >r eec@ swap pST 1+ r> 1+  NEXT  2drop
             THEN
   r> pLD 1- p@ or IF  0 noop BRANCH  THEN  \ Fetching an instruction from address zero toggles warmboot
   BEGIN  <switch LED on>  delay            \ starts flashing when no bootable image
          <switch LED off> delay            \ found in the E2PROM
   REPEAT
; noexit

#reset TRAP: rst     boot ;
#psr   TRAP: psr     noop ; \ 'retry' instruction

end

Boot-file ..\vhdl\bootload.vhd .( bootload.fs written to bootload.vhd )

