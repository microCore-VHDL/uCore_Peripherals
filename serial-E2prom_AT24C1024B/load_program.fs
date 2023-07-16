\ 
\ MicroCore load screen for the core test program that is transferred
\ into the program memory via the debug umbilical
\
Only Forth also definitions

[IFDEF] unpatch     unpatch    [ENDIF]
[IFDEF] close-port  close-port [ENDIF]
[IFDEF] microcore   microcore  [ENDIF]   Marker microcore

include extensions.fs           \ Some System word (re)definitions for a more sympathetic environment
include ../vhdl/architecture_pkg.vhd
include microcross.fs           \ the cross-compiler

Target new initialized          \ go into target compilation mode and initialize target compiler

8 trap-addr code-origin
          0 data-origin

include constants.fs            \ MicroCore Register addresses and bits
include debugger.fs

\ -----------------------------------------------------------------------
\ EEprom support
\ -----------------------------------------------------------------------

: unpack  ( u -- c u' )   dup $FF and swap -8 shift ;
: pack    ( c u -- u' )   8 shift swap $FF and or ;

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

\ -----------------------------------------------------------------------
\ 16 bit CRC
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
\ -----------------------------------------------------------------------
\ install file as boot image
\ -----------------------------------------------------------------------

: host>eeprom  ( -- )   host> host>   \ addr length
   ?FOR  host> over eec!  1+  NEXT  drop
;
Host

: install   ( <filename> -- )  \ transfers file as bootable image into Eeprom
   BL word dup c@ 0= abort" file name required."
   count r/o open-file abort" file does not exist." >r
   T [ #c_rom #c_wp H or invert ] Literal CTRL_REG t_!     \ write protect off
   r@ file-size 2drop                                      \ size in bytes
   [t'] host>eeprom >target
      T #boot-mem H >target   dup >target          ( length )
      r>   swap 0 ?DO  >r  here 1 r@ read-file abort" file exhausted" drop
                           here c@ >target  r>
                           I $7F and 0= IF  ." ."  THEN
                  LOOP
   ?OK
   close-file drop
   T #c_rom ctrl_reg H t_!                         \ write protect on again
;
Target
   
: boot  ( -- )   debug-service ;

#reset TRAP: rst    ( -- )            boot              ;  \ compile branch to boot at reset vector location
#isr   TRAP: isr    ( -- )            IRET              ;
#psr   TRAP: psr    ( -- )            pause             ;  \ call the scheduler, eventually re-execute instruction
#break TRAP: break  ( -- )            debugger          ;  \ Debugger
#does> TRAP: dodoes ( addr -- addr' ) ld 1+ swap BRANCH ;  \ the DOES> runtime primitive
#data! TRAP: data!  ( dp n -- dp+1 )  swap st 1+        ;  \ Data memory initialization

end

boot-image handshake cr .( Load boot.crc into E2prom )  install boot.crc
