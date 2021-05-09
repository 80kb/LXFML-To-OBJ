namespace System.IO
{
    /// <summary>
    /// Specifies the byte order of an instance of the System.IO.EndianReader class.
    /// </summary>
    public enum Endian
    {
        /// <summary>
        /// The byte order is reversed on little-endian systems.
        /// </summary>
        Big,
        /// <summary>
        /// The byte order is reversed on big-endian systems.
        /// </summary>
        Little 
    }

    /// <summary>
    /// Reads primitive data types as binary values in a specific encoding and byte order
    /// </summary>
    class EndianReader : BinaryReader
    {
        private Endian Endianness;

        /// <summary>
        /// Initializes a new instance of the System.IO.EndianReader class based on the specified stream and using UTF-8 encoding.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        public EndianReader(Stream stream) : base(stream)
        {
            Endianness = Endian.Big;
        }

        /// <summary>
        /// Initializes a new instance of the System.IO.EndianReader class based on the specified stream, specified endianness, and using UTF-8 encoding.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="endianness">Relative endianness.</param>
        public EndianReader(Stream stream, Endian endianness) : base(stream) 
        {
            if(BitConverter.IsLittleEndian)
                Endianness = (endianness == Endian.Little) ? Endian.Big : Endian.Little;
            else
                Endianness = (endianness == Endian.Little) ? Endian.Little : Endian.Big;
        }

        public override int ReadInt32()
        {
            var data = base.ReadBytes(4);

            if(Endianness == Endian.Little)
                Array.Reverse(data);

            return BitConverter.ToInt32(data, 0);
        }

        public override short ReadInt16()
        {
            var data = base.ReadBytes(2);

            if (Endianness == Endian.Little)
                Array.Reverse(data);

            return BitConverter.ToInt16(data, 0);
        }

        public override long ReadInt64()
        {
            var data = base.ReadBytes(8);

            if (Endianness == Endian.Little)
                Array.Reverse(data);

            return BitConverter.ToInt64(data, 0);
        }

        public override uint ReadUInt32()
        {
            var data = base.ReadBytes(4);

            if (Endianness == Endian.Little)
                Array.Reverse(data);

            return BitConverter.ToUInt32(data, 0);
        }

        public override float ReadSingle()
        {
            var data = base.ReadBytes(4);

            if (Endianness == Endian.Little)
                Array.Reverse(data);

            return BitConverter.ToSingle(data, 0);
        }
    }
}