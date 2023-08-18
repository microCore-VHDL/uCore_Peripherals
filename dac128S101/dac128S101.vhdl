-----------------------------------------------------------------
-- dac128S101.vhd
-----------------------------------------------------------------
--
-- Author: KLAUS SCHLEISIEK
-- Last change: KS 18.08.2023 18:53:14
--
-- Interface for the DAC121S101 12-bit DAC
-- There is one register DAC_REG. <value> DAC-reg ! starts a conversion.
-- While busy = '1', writing to DAC_REG raises dac_pause.
-- dac_pause has to be ORed into uBus.pause in fpga.vhd.
--
-- dac_din is sampled on the falling sclk edge

LIBRARY IEEE;
USE IEEE.STD_LOGIC_1164.ALL;
USE IEEE.NUMERIC_STD.ALL;
USE work.functions_pkg.ALL;
USE work.architecture_pkg.ALL;

ENTITY DAC121S101 IS PORT (
   uBus        : IN  uBus_port;
   sclk_en     : IN  STD_LOGIC; -- enable signal just before rising edge of sclk
   dac_pause   : OUT STD_LOGIC; -- raises pause when writing DAC while it is busy
-- DAC121S101 interface
   dac_cs      : OUT STD_LOGIC;
   dac_din     : OUT STD_LOGIC
); END DAC121S101;

ARCHITECTURE rtl OF DAC121S101 IS

ALIAS  reset       : STD_LOGIC IS uBus.reset;
ALIAS  clk         : STD_LOGIC IS uBus.clk;
ALIAS  wdata       : data_bus  IS uBus.wdata;

SIGNAL busy        : STD_LOGIC;
SIGNAL ready       : STD_LOGIC;
SIGNAL dac_wr      : STD_LOGIC;
SIGNAL shifter     : UNSIGNED(15 DOWNTO 0);
SIGNAL dac_ctr     : NATURAL RANGE 16 DOWNTO 0;

BEGIN

dac_cs <= busy;
dac_din <= shifter(15);
dac_pause <= '1' WHEN  uReg_write(uBus, DAC_REG) AND busy = '1'  ELSE '0';

busy <= '1' WHEN  dac_ctr /= 0  ELSE '0';

set_dac_proc : PROCESS (clk)
BEGIN
   IF  rising_edge(clk)  THEN
      IF  uReg_write(uBus, DAC_REG) AND busy = '0'  THEN
         shifter <= "0000" & wdata(11 DOWNTO 0);
         dac_wr <= '1';
      END IF;
      IF  sclk_en = '1'  THEN
         IF  busy = '1'  THEN
            shifter <= shifter(14 DOWNTO 0) & '0';
            dac_ctr <= dac_ctr - 1;
         ELSIF  dac_wr = '1'  THEN
            dac_wr <= '0';
            dac_ctr <= 16;
         END IF;
      END IF;
      IF  reset = '1'  THEN
         dac_wr <= '0';
         shifter(15) <= '0';
         dac_ctr <= 0;
      END IF;
   END IF;
END PROCESS set_dac_proc;

END rtl;
