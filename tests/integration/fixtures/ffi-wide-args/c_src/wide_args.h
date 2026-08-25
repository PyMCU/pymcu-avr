#pragma once
#include <stdint.h>

/*
 * Probes for the DECLARED width of an @extern parameter.
 *
 * Both functions read all 16 bits of every argument, so a caller that loads only
 * the low half of a register pair is caught as a wrong result rather than as a
 * link error. Passing small literals is the interesting case: their value fits
 * in one byte, but the parameter does not.
 */

/* Returns a + b + c, so a stale high half in any argument register shows up. */
uint16_t wide_sum3(uint16_t a, uint16_t b, uint16_t c);

/* Echoes arg0 back; used to leave large values in the argument registers. */
uint16_t wide_echo0(uint16_t a, uint16_t b, uint16_t c);

/* Echoes a 32-bit arg0. avr-gcc reads it as byte0 in R22 .. byte3 in R25; a caller
 * using PyMCU's own R24-anchored layout hands it the two 16-bit halves swapped. */
uint32_t wide_echo32(uint32_t a);

/* a + b, with the 32-bit argument in the SECOND slot, which is contiguous either way. */
uint32_t wide_sum32(uint32_t a, uint32_t b);

/* (uint16_t)(x * k). A float parameter that arrives as an integer bit pattern
 * gives a wildly different product, so truncation on the way in is visible. */
uint16_t wide_scale_to_u16(float x, float k);
