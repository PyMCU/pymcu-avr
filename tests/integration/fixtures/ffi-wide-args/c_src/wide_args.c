/*
 * wide_args.c -- 16-bit parameter probes for PyMCU @extern FFI.
 *
 * See wide_args.h. The (void) casts suppress unused-parameter warnings.
 */

#include "wide_args.h"

uint16_t wide_sum3(uint16_t a, uint16_t b, uint16_t c)
{
    return (uint16_t)(a + b + c);
}

uint16_t wide_echo0(uint16_t a, uint16_t b, uint16_t c)
{
    (void)b; (void)c;
    return a;
}

uint32_t wide_echo32(uint32_t a)
{
    return a;
}

uint32_t wide_sum32(uint32_t a, uint32_t b)
{
    return a + b;
}

uint16_t wide_scale_to_u16(float x, float k)
{
    return (uint16_t)(x * k);
}
