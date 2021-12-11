﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritanport
{
    internal class Wave : MemoryStream, ICloneable
    {
        public TimeSpan PlayTime => Length > 0 ? new(10_000_000L * DataLength / BPS) : new(0);

        private const int WAVE_SIZE = 4;
        private const int WAVE_CHANNEL = 22;
        private const int WAVE_SAMPLING = 24;
        private const int WAVE_BPS = 28;
        private const int WAVE_BLOCK = 32;
        private const int WAVE_DEPTH = 34;

        /// <summary>
        /// Waveファイルのサイズ - 8byte
        /// Lengthと一致しない場合もある（FileLength + 8 ≦ Length）
        /// </summary>
        private int FileLength
        {
            get
            {
                return GetParamater(WAVE_SIZE, false);
            }
            set
            {
                long p = Position;
                Position = WAVE_SIZE;
                Write(BitConverter.GetBytes(value));
                Position = p;
            }
        }
        private short Channel => (short)GetParamater(WAVE_CHANNEL, true);
        private int Sampling => GetParamater(WAVE_SAMPLING, false);
        private int BPS => GetParamater(WAVE_BPS, false);
        private short Block => (short)GetParamater(WAVE_BLOCK, true);
        private short Depth => (short)GetParamater(WAVE_DEPTH, true);

        /// <summary>
        /// 生データのサイズ
        /// 変更する場合はBlockの整数倍になる様にすること（FileLengthの変更も忘れずに）
        /// </summary>
        private int DataLength
        {
            get
            {
                return GetParamater(DataOffset + 4, false);
            }
            set
            {
                long p = Position;
                Position = DataOffset + 4;
                Write(BitConverter.GetBytes(value));
                Position = p;
            }
        }
        /// <summary>
        /// data識別子の位置
        /// [size] (4byte) -> offset + 4 ~ offset + 7
        /// [data] ([size]byte) -> offset + 8 ~
        /// </summary>
        private int DataOffset
        {
            get
            {
                if (Length < 40)
                {
                    return -1;
                }
                byte[] buffer = new byte[4];

                long p = Position;

                for (int i = 36; i < Length; i++)
                {
                    Position = i;
                    Read(buffer);
                    if (Encoding.UTF8.GetString(buffer) == "data")
                    {
                        Position = p;
                        return i;
                    }
                }
                Position = p;
                return -1;
            }
        }

        private int GetParamater(int offset, bool IsInt16)
        {
            if (Length < offset + 4)
            {
                return -1;
            }

            byte[] buffer = new byte[4];

            long p = Position;
            Position = offset;
            Read(buffer);
            Position = p;

            if (IsInt16)
            {
                return BitConverter.ToInt16(buffer);
            }
            return BitConverter.ToInt32(buffer);
        }

        public Wave() : base() { }

        public Wave(in MemoryStream wavData) : base()
        {
            Write(wavData.ToArray());
        }

        public void CreateSilence(double seconds)
        {
            int length = (int)(BPS * seconds) - (int)(BPS * seconds) % Block + Block;

            byte[] silence = new byte[length];

            Position = Length;
            Write(silence);

            FileLength += length;
            DataLength += length;
        }
        /// <summary>
        /// Waveデータを追加する
        /// 追加先が空だった場合は追加元のコピーになる
        /// </summary>
        /// <param name="src">追加するWave</param>
        /// <returns>追加に成功した場合はtrue（フォーマット不一致の場合はfalse)</returns>
        public bool Append(Wave src)
        {
            Wave dst = this;

            if(dst.Length == 0)
            {
                src.CopyTo(dst);

                return true;
            }

            if (src == dst)
            {
                throw new("self appending is not supported.");
            }

            if (src.DataOffset != dst.DataOffset)
            {
                return false;
            }

            //フォーマットの比較
            byte[] src_header = new byte[src.DataOffset];
            byte[] dst_header = new byte[dst.DataOffset];

            src.Position = 0;
            dst.Position = 0;
            src.Read(src_header);
            dst.Read(dst_header);

            for (int i = 8; i < src.DataOffset; i++)
            {
                if (src_header[i] != dst_header[i])
                {
                    return false;
                }
            }

            dst.Position = dst.FileLength;
            dst.Write(src.ToArray(), src.DataOffset + 8, src.DataLength);

            dst.FileLength += src.DataLength;
            dst.DataLength += src.DataLength;

            return true;
        }

        public object Clone()
        {
            return new Wave(this);
        }

        public void Save(string path)
        {
            if (!path.EndsWith(".wav"))
            {
                path += ".wav";
            }
            BinaryWriter writer = new(new FileStream(path, FileMode.CreateNew));
            writer.Write(ToArray());
            writer.Close();
            writer.Dispose();
        }
    }
}
