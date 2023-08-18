-----------------------------------------------------------------
-- adc128S102                                                  --
-----------------------------------------------------------------
--
-- Author: KLAUS SCHLEISIEK
-- Last change: KS 18.08.2023 17:54:54
--
-- Interface for the ADC128S102 12-bit, 8 channel AD converter
--
--------------------------------------------------------------------------
-- Functional description of the interface:
--
-- There is one register ADC_REG. '<channel> ADC-reg !' starts conversion
-- of <channel>. 'ADC-reg @' reads the conversion result(s) (see below).
-- As long as busy  = 1, adc_pause will be raised when writing ADC_REG.
-- As long as ready = 0, adc_pause will be raised when reading ADC_REG.
--
-- Conversion results:
-- SAMPLE1 is the result of channel(2 DOWNTO 0) of the CURRENT conversion.
-- SAMPLE0 is the result of the first conversion whose address had been
-- set in channel(6 DOWNTO 3) during the PREVIOUS conversion. Most of the
-- time, SAMPLE0 will not be needed. It can be used for high sample rates.
--------------------------------------------------------------------------

LIBRARY IEEE;
USE IEEE.STD_LOGIC_1164.ALL;
USE IEEE.NUMERIC_STD.ALL;
USE work.functions_pkg.ALL;
USE work.architecture_pkg.ALL;

ENTITY ADC128S102 IS PORT (
   uBus        : IN  uBus_port;
   sclk_en     : IN  STD_LOGIC; -- enable signal just before rising edge of sclk
   adc_pause   : OUT STD_LOGIC; -- raises pause when writing or reading the ADC while it is busy or not ready.
   sample0     : OUT UNSIGNED(11 DOWNTO 0); -- channel number set in previous conversion
   sample1     : OUT UNSIGNED(11 DOWNTO 0); -- new channel number
-- ADC128S102 interface
   adc_cs      : OUT STD_LOGIC;
   adc_din     : OUT STD_LOGIC; -- data written to ADC
   adc_dout    : IN  STD_LOGIC  -- data read from ADC
); END ADC128S102;

ARCHITECTURE rtl OF ADC128S102 IS

ALIAS  reset       : STD_LOGIC IS uBus.reset;
ALIAS  clk         : STD_LOGIC IS uBus.clk;
ALIAS  wdata       : data_bus  IS uBus.wdata;

SIGNAL channel     : UNSIGNED( 5 DOWNTO 0);
SIGNAL shift_out   : UNSIGNED( 4 DOWNTO 0);
SIGNAL shift_in    : UNSIGNED(10 DOWNTO 0);
SIGNAL cycle       : NATURAL RANGE 0 TO 31;
SIGNAL start       : STD_LOGIC;
SIGNAL busy        : STD_LOGIC; -- 1 after start of conversion, 0 after reading the sample(s)
SIGNAL ready       : STD_LOGIC; -- 1 after end of conversion, 0 while converting

BEGIN

adc_cs <= busy;
adc_din <= shift_out(4);
adc_pause <= '1' WHEN  (uReg_write(uBus, ADC_REG) AND busy = '1') OR
                       (uReg_read (uBus, ADC_REG) AND ready = '0')
             ELSE '0';

adc_ctrl_proc : PROCESS (clk)
BEGIN
   IF  rising_edge(clk)  THEN
      IF  uReg_write(uBus, ADC_REG) AND busy = '0'  THEN
         start <= '1';
         ready <= '0';
         channel <= wdata(5 DOWNTO 0);
      END IF;
      IF  uReg_read (uBus, ADC_REG) AND ready = '1'  THEN
         busy <= '0';
      END IF;
      IF  sclk_en = '1'  THEN
         IF  busy = '0'  THEN
            IF  start = '1'  THEN
               start <= '0';
               shift_out <= "00" & channel(2 DOWNTO 0);
               busy <= '1';
               cycle <= 31;
            END IF;
         ELSE --  busy = '1'
            shift_out <= shift_out(3 DOWNTO 0) & '0';  -- channel of sample1 of the current conversion
            shift_in <= shift_in(9 DOWNTO 0) & adc_dout;
            IF  cycle = 0  THEN
               ready <= '1';
               sample1 <= shift_in & adc_dout;
            ELSE
               cycle <= cycle - 1;
            END IF;
            IF  cycle = 16  THEN
               shift_out <= "00" & channel(5 DOWNTO 3); -- channel of sample0 after the next conversion
               sample0 <= shift_in & adc_dout;
            END IF;
         END IF;
      END IF;
      IF  reset = '1'  THEN
         start <= '0';
         busy <= '0';
         ready <= '1';
         shift_out(4) <= '0';
      END IF;
   END IF;
END PROCESS adc_ctrl_proc;

END rtl;
