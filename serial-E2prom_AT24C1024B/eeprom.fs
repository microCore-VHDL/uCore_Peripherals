\ ----------------------------------------------------------------------
\ @file : eeprom.fs
\ ----------------------------------------------------------------------
\
\ Project : microCore
\ Language : gforth_0.6.2
\ Last check in : $Rev: 619 $ $Date:: 2021-01-20 #$
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
\ @brief : This is the interface code for a serial E2PROM, which has to
\          be loaded as part of your application in the Target.
\          Please not that for this code to work your system must have
\          a data_width of at least 18 bits!
\
\ Version Author   Date       Changes
\   2300    ks   19-Feb-2021  Initial release
\ ----------------------------------------------------------------------
Target

: unpack  ( u -- c u' )   dup $FF and swap -8 shift ; \ libloading inside Host: does not work
: pack    ( c u -- u' )   8 shift swap $FF and or ;   \ libloading inside Host: does not work

Host: eeunpacks ( -- )
   #bytes/cell 1- 0 ?DO  T unpack H LOOP
   #bytes/cell 1- 0 ?DO  T >r     H LOOP
;
Host: eestores  ( -- )  #bytes/cell 1- 0 ?DO  T st r> swap H LOOP ;
Host: eeloads   ( -- )  #bytes/cell 1- 0 ?DO  T ld   H LOOP ;
Host: eepacks   ( -- )  #bytes/cell 1- 0 ?DO  T pack H LOOP ;

: eec!   ( 8b addr -- )   EE-addr st   2 - ! ;

: ee!    ( n addr -- )
   swap eeunpacks swap EE-addr st  1- eestores 1- !
;
: ee_set ( addr1 -- addr2 )  $20000 or EE-addr st ;

: eec@   ( addr -- 8b )      ee_set 2 - @ ;

: ee@    ( addr -- n )       ee_set 1- eeloads 1- @ eepacks ;

: >eeprom ( from.mem to.ee quan -- )
   ?FOR  over @ over ee!  #bytes/cell + swap 1+ swap  NEXT  2drop
;
: eeprom> ( from.ee to.mem quan -- )
   ?FOR  over ee@ over !  1+ swap #bytes/cell + swap  NEXT  2drop
;
Host

: t_eec@  ( addr -- byte )   >t [t'] eec@ t_execute t> ;

Command definitions

: eedump   ( T addr len -- )
   temp-hex   t> t> swap bounds
   ?DO  cr I .addr
        I $10 bounds DO  I t_eec@ 3 u.r  LOOP
   $10 +LOOP space
;
Target
\ -----------------------------------------------------------------------
\ CRC16-CCITT
\ Width = 16 bits
\ Truncated polynomial = 0x1021
\ Initial value = 0xFFFF
\ CRC computed following the algorithm specified in the ECSS-E-70-41A
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
: bootcode?  ( -- f )  \ true when E2prom contains a bootable image with matching CRC
   #boot-mem dup ee@ ?dup IF  swap #bytes/cell + swap #bytes/crc + crc-check  THEN  0=
;
: unboot  ( -- )  \ invalidate E2prom boot image
   #c_rom -Ctrl !   0 #boot-mem ee!   #c_rom Ctrl !
;
\ -----------------------------------------------------------------------
\ E2prom variables
\
\ EE Variable <name> defines a variable in the E2prom below #rom-mem.
\ EEp is the allocation pointer initialized in constants.fs
\
\ ROM Variable <name> defines a variable in the E2prom above #rom-mem.
\ This area is safeguarded by the #c_rom Ctrl-reg bit.
\ ROMp is the allocation pointer initialized in constants.fs
\
\ Both classes of variables can be accessed using methods @ and !, which
\ actually do a call to ee@ and ee!.
\ -----------------------------------------------------------------------

Class EE   EE definitions   
#bytes/cell EE allot   EE seal

Host: Variable ( <name> -- )   EEp (object ;
Host: allot    ( units -- )    Self size * EEp +! ;

    : @   ( addr -- n )  ee@ ;
    : !   ( n addr -- )  ee! ;
Host: ?   ( addr -- )    ?dbg T ee@ dup u. . H ;

Target

Class ROM  ROM definitions
EE inherit   ROM seal

Host: Variable ( <name> -- )   ROMp (object ;
Host: allot    ( units -- )    Self size * ROMp +! ;

Target
