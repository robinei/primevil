using System;

namespace Primevil.Formats
{
    /*
     * Decode PKWare Compression Library stream.
     *
     * Format notes:
     *
     * - First byte is 0 if literals are uncoded or 1 if they are coded.  Second
     *   byte is 4, 5, or 6 for the number of extra bits in the distance code.
     *   This is the base-2 logarithm of the dictionary size minus six.
     *
     * - Compressed data is a combination of literals and length/distance pairs
     *   terminated by an end code.  Literals are either Huffman coded or
     *   uncoded bytes.  A length/distance pair is a coded length followed by a
     *   coded distance to represent a string that occurs earlier in the
     *   uncompressed data that occurs again at the current location.
     *
     * - A bit preceding a literal or length/distance pair indicates which comes
     *   next, 0 for literals, 1 for length/distance.
     *
     * - If literals are uncoded, then the next eight bits are the literal, in the
     *   normal bit order in th stream, i.e. no bit-reversal is needed. Similarly,
     *   no bit reversal is needed for either the length extra bits or the distance
     *   extra bits.
     *
     * - Literal bytes are simply written to the output.  A length/distance pair is
     *   an instruction to copy previously uncompressed bytes to the output.  The
     *   copy is from distance bytes back in the output stream, copying for length
     *   bytes.
     *
     * - Distances pointing before the beginning of the output data are not
     *   permitted.
     *
     * - Overlapped copies, where the length is greater than the distance, are
     *   allowed and common.  For example, a distance of one and a length of 518
     *   simply copies the last byte 518 times.  A distance of four and a length of
     *   twelve copies the last four bytes three times.  A simple forward copy
     *   ignoring whether the length is greater than the distance or not implements
     *   this correctly.
     */
    public struct PkUnzipper
    {
        private const int MaxBits = 13;


        // input state
        private byte[] inBuf;
        private int inSize;
        private int inPos;
        private int bitBuf;
        private int bitCount;


        // assumes that the whole stream is in "buffer", and that the result will
        // fit in "outBuf"
        public uint Decompress(byte[] buffer, int offset, int size, byte[] outBuf)
        {
            inBuf = buffer;
            inPos = offset;
            inSize = size;

            bitBuf = 0;
            bitCount = 0;

            int outPos = 0;

            int lit;            // true if literals are coded
            int dict;           // log2(dictionary size) - 6
            int symbol;         // decoded symbol, extra bits for distance
            int len;            // length for copy
            int dist;           // distance for copy
            int copy;           // copy counter


            // read header
            lit = Bits(8);
            if (lit > 1)
                throw new Exception("invalid header data");
            dict = Bits(8);
            if (dict < 4 || dict > 6)
                throw new Exception("invalid header data");

            // decode literals and length/distance pairs
            while(true) {
                if (Bits(1) != 0) {
                    // get length
                    symbol = Decode(ref lencode);
                    len = lenbase[symbol] + Bits(extra[symbol]);
                    if (len == 519)
                        break; // end code

                    // get distance
                    symbol = len == 2 ? 2 : dict;
                    dist = Decode(ref distcode) << symbol;
                    dist += Bits(symbol);
                    dist++;
                    if (dist > outPos) // distance too far back
                        throw new Exception("invalid distance");

                    // copy length bytes from distance bytes back
                    do {
                        int fromPos = outPos - dist;
                        copy = outBuf.Length;
                        if (outPos < dist) {
                            fromPos += copy;
                            copy = dist;
                        }
                        copy -= outPos;
                        if (copy > len) copy = len;
                        len -= copy;
                        do {
                            outBuf[outPos++] = outBuf[fromPos++];
                        } while (--copy > 0);
                    } while (len != 0);
                }
                else {
                    // get literal and write it
                    symbol = (lit != 0) ? Decode(ref litcode) : Bits(8);
                    outBuf[outPos++] = (byte)symbol;
                }
            }
            
            return (uint)outPos;
        }


        /*
         * Decode a code from the stream s using huffman table h.  Return the symbol or
         * a negative value if there is an error.  If all of the lengths are zero, i.e.
         * an empty code, or if the code is incomplete and an invalid code is received,
         * then -9 is returned after reading MAXBITS bits.
         *
         * Format notes:
         *
         * - The codes as stored in the compressed data are bit-reversed relative to
         *   a simple integer ordering of codes of the same lengths.  Hence below the
         *   bits are pulled from the compressed data one at a time and used to
         *   build the code value reversed from what is in the stream in order to
         *   permit simple integer comparisons for decoding.
         *
         * - The first code for the shortest length is all ones.  Subsequent codes of
         *   the same length are simply integer decrements of the previous code.  When
         *   moving up a length, a one bit is appended to the code.  For a complete
         *   code, the last code of the longest length will be all zeros.  To support
         *   this ordering, the bits pulled during decoding are inverted to apply the
         *   more "natural" ordering starting with all zeros and incrementing.
         */
        private int Decode(ref Huffman h)
        {
            int len = 1;            // current number of bits in code
            int code = 0;           // len bits being decoded
            int first = 0;          // first code of length len
            int index = 0;          // index of first code of length len in symbol table
            int bits = bitBuf;      // bits from stream
            int left = bitCount;    // bits left in next or left to process
            int next = 1;           // index into next number of codes

            while (true) {
                while (left-- > 0) {
                    code |= (bits & 1) ^ 1;   // invert code
                    bits >>= 1;
                    int count = h.Count[next++]; // number of codes of length len
                    if (code < first + count) { // if length len, return symbol
                        bitBuf = bits;
                        bitCount = (bitCount - len) & 7;
                        return h.Symbol[index + (code - first)];
                    }
                    index += count; // else update for next length
                    first += count;
                    first <<= 1;
                    code <<= 1;
                    len++;
                }
                left = (MaxBits + 1) - len;
                if (left == 0)
                    break;
                bits = inBuf[inPos++];
                if (left > 8)
                    left = 8;
            }
            return -9; /* ran out of codes */
        }


        /*
         * Return need bits from the input stream.  This always leaves less than
         * eight bits in the buffer.  bits() works properly for need == 0.
         *
         * Format notes:
         *
         * - Bits are stored in bytes from the least significant bit to the most
         *   significant bit.  Therefore bits are dropped from the bottom of the bit
         *   buffer, using shift right, and new bytes are appended to the top of the
         *   bit buffer, using shift left.
         */
        private int Bits(int need)
        {
            int val = bitBuf; // bit accumulator

            // load at least need bits into val
            while (bitCount < need) {
                val |= inBuf[inPos++] << bitCount; // load eight bits
                bitCount += 8;
            }

            // drop need bits and update buffer, always zero to seven bits left
            bitBuf = val >> need;
            bitCount -= need;

            // return need bits, zeroing the bits above that
            return val & ((1 << need) - 1);
        }







        // bit lengths of literal codes
        private static readonly byte[] litlen = {
            11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
            9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
            7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
            8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
            44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
            44, 173};

        // bit lengths of length codes 0..15
        private static readonly byte[] lenlen = { 2, 35, 36, 53, 38, 23 };

        // bit lengths of distance codes 0..63
        private static readonly byte[] distlen = { 2, 20, 53, 230, 247, 151, 248 };

        private static readonly short[] lenbase = {     /* base for length codes */
            3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264};

        private static readonly byte[] extra = {     /* extra bits for length codes */
            0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8};

        internal struct Huffman
        {
            public short[] Count;
            public short[] Symbol;
        }

        private static Huffman litcode;
        private static Huffman lencode;
        private static Huffman distcode;

        static PkUnzipper()
        {
            litcode.Count = new short[MaxBits + 1];
            litcode.Symbol = new short[256];
            Construct(ref litcode, litlen);

            lencode.Count = new short[MaxBits + 1];
            lencode.Symbol = new short[16];
            Construct(ref lencode, lenlen);

            distcode.Count = new short[MaxBits + 1];
            distcode.Symbol = new short[64];
            Construct(ref distcode, distlen);
        }

        /*
         * Given a list of repeated code lengths rep[0..n-1], where each byte is a
         * count (high four bits + 1) and a code length (low four bits), generate the
         * list of code lengths.  This compaction reduces the size of the object code.
         * Then given the list of code lengths length[0..n-1] representing a canonical
         * Huffman code for n symbols, construct the tables required to decode those
         * codes.  Those tables are the number of codes of each length, and the symbols
         * sorted by length, retaining their original order within each length.  The
         * return value is zero for a complete code set, negative for an over-
         * subscribed code set, and positive for an incomplete code set.  The tables
         * can be used if the return value is zero or positive, but they cannot be used
         * if the return value is negative.  If the return value is zero, it is not
         * possible for decode() using that table to return an error--any stream of
         * enough bits will resolve to a symbol.  If the return value is positive, then
         * it is possible for decode() using that table to return an error for received
         * codes past the end of the incomplete lengths.
         */
        private static int Construct(ref Huffman h, byte[] rep)
        {
            int symbol;         /* current symbol when stepping through length[] */
            int len;            /* current length when stepping through h->count[] */
            int left;           /* number of possible codes left of current length */
            var offs = new short[MaxBits + 1];      /* offsets in symbol table for each length */
            var length = new short[256];  /* code lengths */
            int repPos = 0;

            /* convert compact repeat counts into symbol bit length list */
            symbol = 0;
            while (repPos < rep.Length) {
                len = rep[repPos++];
                left = (len >> 4) + 1;
                len &= 15;
                do {
                    length[symbol++] = (short)len;
                } while (--left > 0);
            }

            int n = symbol;

            /* count number of codes of each length */
            for (len = 0; len <= MaxBits; len++)
                h.Count[len] = 0;
            for (symbol = 0; symbol < n; symbol++)
                (h.Count[length[symbol]])++;   /* assumes lengths are within bounds */
            if (h.Count[0] == n)               /* no codes! */
                return 0;                       /* complete, but decode() will fail */

            /* check for an over-subscribed or incomplete set of lengths */
            left = 1;                           /* one possible code of zero length */
            for (len = 1; len <= MaxBits; len++) {
                left <<= 1;                     /* one more bit, double codes left */
                left -= h.Count[len];          /* deduct count from possible codes */
                if (left < 0)
                    return left;      /* over-subscribed--return negative */
            }                                   /* left > 0 means incomplete */

            /* generate offsets into symbol table for each length for sorting */
            offs[1] = 0;
            for (len = 1; len < MaxBits; len++)
                offs[len + 1] = (short)(offs[len] + h.Count[len]);

            /*
             * put symbols in table sorted by length, by symbol order within each
             * length
             */
            for (symbol = 0; symbol < n; symbol++)
                if (length[symbol] != 0)
                    h.Symbol[offs[length[symbol]]++] = (short)symbol;

            /* return zero for complete set, positive for incomplete set */
            return left;
        }
    }
}
