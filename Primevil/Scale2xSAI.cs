namespace Primevil
{
    class Scale2xSAI
    {
        static int GetResult(uint a, uint b, uint c, uint d)
        {
            int x = 0; 
            int y = 0;
            int r = 0;

            if (a == c) x += 1; else if (b == c) y += 1;
            if (a == d) x += 1; else if (b == d) y += 1;
            if (x <= 1) r += 1; 
            if (y <= 1) r -= 1;

            return r;
        }

        static uint Interpolate(uint a, uint b)
        {
            if (a == b)
                return a;
            return  ((a & 0xFEFEFEFE) >> 1) + 
                    (((b & 0xFEFEFEFE) >> 1) |
                    (a & b & 0x01010101));
        }

        static uint QInterpolate(uint a, uint b, uint c, uint d)
        {
            uint x = ((a & 0xFCFCFCFC) >> 2) +
                     ((b & 0xFCFCFCFC) >> 2) +
                     ((c & 0xFCFCFCFC) >> 2) +
                     ((d & 0xFCFCFCFC) >> 2);
            uint y = (((a & 0x03030303) +
                     (b & 0x03030303) +
                     (c & 0x03030303) +
                     (d & 0x03030303)) >> 2) & 0x03030303;
            return x | y;
        }

        public static void Super2xSaI(uint[] src, uint[] dest, int width, int height, int pitch = 0)
        {
            if (pitch == 0)
                pitch = width;
            int destWidth = width << 1;
            int srcPos = 0;
            int destPos = 0;

            // ReSharper disable TooWideLocalVariableScope
            uint color4, color5, color6;
            uint color1, color2, color3;
            uint colorA0, colorA1, colorA2, colorA3;
            uint colorB0, colorB1, colorB2, colorB3;
            uint colorS1, colorS2;
            uint product1a, product1b, product2a, product2b;

            int row0, row1, row2, row3;
            int col0, col1, col2, col3;
            int y, x;
            // ReSharper restore TooWideLocalVariableScope

            for (y = 0; y < height; y++) {
                if (y > 0) {
                    row0 = width;
                    row0 = -row0;
                } else
                    row0 = 0;

                row1 = 0;

                if (y < height - 1) {
                    row2 = width;

                    if (y < height - 2) 
                        row3 = width << 1;
                    else
                        row3 = width;
                } else {
                    row2 = 0;
                    row3 = 0;
                }

                for (x = 0; x < width; x++) {
        //--------------------------------------- B0 B1 B2 B3
        //                                         4  5  6 S2
        //                                         1  2  3 S1
        //                                        A0 A1 A2 A3
                    if (x > 0)
                        col0 = -1;
                    else
                        col0 = 0;

                    col1 = 0;

                    if (x < width - 1) {
                        col2 = 1;
                        col3 = x < width - 2 ? 2 : 1;
                    } else {
                        col2 = 0;
                        col3 = 0;
                    }

                    colorB0 = src[srcPos + col0 + row0];
                    colorB1 = src[srcPos + col1 + row0];
                    colorB2 = src[srcPos + col2 + row0];
                    colorB3 = src[srcPos + col3 + row0];

                    color4 = src[srcPos + col0 + row1];
                    color5 = src[srcPos + col1 + row1];
                    color6 = src[srcPos + col2 + row1];
                    colorS2 = src[srcPos + col3 + row1];

                    color1 = src[srcPos + col0 + row2];
                    color2 = src[srcPos + col1 + row2];
                    color3 = src[srcPos + col2 + row2];
                    colorS1 = src[srcPos + col3 + row2];

                    colorA0 = src[srcPos + col0 + row3];
                    colorA1 = src[srcPos + col1 + row3];
                    colorA2 = src[srcPos + col2 + row3];
                    colorA3 = src[srcPos + col3 + row3];

        //--------------------------------------
                    if (color2 == color6 && color5 != color3)
                        product2b = product1b = color2;
                    else if (color5 == color3 && color2 != color6)
                        product2b = product1b = color5;
                    else if (color5 == color3 && color2 == color6) {
                        int r = 0;

                        r += GetResult (color6, color5, color1, colorA1);
                        r += GetResult (color6, color5, color4, colorB1);
                        r += GetResult (color6, color5, colorA2, colorS1);
                        r += GetResult (color6, color5, colorB2, colorS2);

                        if (r > 0)
                            product2b = product1b = color6;
                        else if (r < 0)
                            product2b = product1b = color5;
                        else
                            product2b = product1b = Interpolate (color5, color6);
                    } else {

                        if (color6 == color3 && color3 == colorA1 && color2 != colorA2 && color3 != colorA0)
                            product2b = QInterpolate (color3, color3, color3, color2);
                        else if (color5 == color2 && color2 == colorA2 && colorA1 != color3 && color2 != colorA3)
                            product2b = QInterpolate (color2, color2, color2, color3);
                        else
                            product2b = Interpolate (color2, color3);

                        if (color6 == color3 && color6 == colorB1 && color5 != colorB2 && color6 != colorB0)
                            product1b = QInterpolate (color6, color6, color6, color5);
                        else if (color5 == color2 && color5 == colorB2 && colorB1 != color6 && color5 != colorB3)
                            product1b = QInterpolate (color6, color5, color5, color5);
                        else
                            product1b = Interpolate (color5, color6);
                    }

                    if (color5 == color3 && color2 != color6 && color4 == color5 && color5 != colorA2)
                        product2a = Interpolate (color2, color5);
                    else if (color5 == color1 && color6 == color5 && color4 != color2 && color5 != colorA0)
                        product2a = Interpolate(color2, color5);
                    else
                        product2a = color2;

                    if (color2 == color6 && color5 != color3 && color1 == color2 && color2 != colorB2)
                        product1a = Interpolate (color2, color5);
                    else if (color4 == color2 && color3 == color2 && color1 != color5 && color2 != colorB0)
                        product1a = Interpolate(color2, color5);
                    else
                        product1a = color5;

                    dest[destPos] = product1a;
                    dest[destPos + 1] = product1b;
                    dest[destPos + destWidth] = product2a;
                    dest[destPos + destWidth + 1] = product2b;

                    ++srcPos;
                    destPos += 2;
                }
                srcPos += (pitch - width);
                destPos += (((pitch - width) << 1) + (pitch << 1));
            }
        }
    }
}
