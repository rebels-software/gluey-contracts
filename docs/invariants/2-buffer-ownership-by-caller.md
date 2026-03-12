# Invariant 2: Buffer Ownership by Caller

## Rule
Gluey.Contract never copies, reallocates, or takes ownership of the byte buffer. The caller owns the buffer and is responsible for its lifetime.

## Rationale
Copying the buffer would be an allocation. The caller decides whether to use `ArrayPool<byte>`, stack allocation, or a managed array.

## Verification
- No `Array.Copy`, `ToArray()`, `new byte[]`, or `MemoryStream` in parser code.
- `ParsedProperty` holds offset + length, never a byte array reference of its own.
