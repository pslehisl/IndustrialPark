﻿using HipHopFile;
using SharpDX;
using System.ComponentModel;

namespace IndustrialPark
{
    public class AssetPEND : EntityAsset
    {
        public static bool dontRender = false;

        public override bool DontRender => dontRender;
        
        public AssetPEND(Section_AHDR AHDR, Game game, Platform platform) : base(AHDR, game, platform) { }

        protected override int EventStartOffset => 0x84 + Offset;

        private const string categoryName = "Pendulum";

        [Category(categoryName)]
        public byte UnknownByte54
        {
            get => ReadByte(0x54 + Offset);
            set => Write(0x54 + Offset, value);
        }

        [Category(categoryName)]
        public byte UnknownByte55
        {
            get => ReadByte(0x55 + Offset);
            set => Write(0x55 + Offset, value);
        }

        [Category(categoryName)]
        public byte UnknownByte56
        {
            get => ReadByte(0x56 + Offset);
            set => Write(0x56 + Offset, value);
        }

        [Category(categoryName)]
        public byte UnknownByte57
        {
            get => ReadByte(0x57 + Offset);
            set => Write(0x57 + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt58
        {
            get => ReadInt(0x58 + Offset);
            set => Write(0x58 + Offset, value);
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        public float MovementDistance
        {
            get => ReadFloat(0x5C + Offset);
            set => Write(0x5C + Offset, value);
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        public float SteepnessRad
        {
            get => ReadFloat(0x60 + Offset);
            set => Write(0x60 + Offset, value);
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        [Description("In degrees")]
        public float Steepness
        {
            get => MathUtil.RadiansToDegrees(ReadFloat(0x60 + Offset));
            set => Write(0x60 + Offset, MathUtil.DegreesToRadians(value));
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        public float MovementTime
        {
            get => ReadFloat(0x64 + Offset);
            set => Write(0x64 + Offset, value);
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        [Description("In radians")]
        [DisplayName("UnknownFloat68")]
        public float UnknownFloat68Rad
        {
            get => ReadFloat(0x68 + Offset);
            set => Write(0x68 + Offset, value);
        }

        [Category(categoryName), TypeConverter(typeof(FloatTypeConverter))]
        [Description("In degrees")]
        [DisplayName("UnknownFloat68")]
        public float UnknownFloat68Deg
        {
            get => MathUtil.RadiansToDegrees(ReadFloat(0x68 + Offset));
            set => Write(0x68 + Offset, MathUtil.DegreesToRadians(value));
        }

        [Category(categoryName)]
        public int UnknownInt6C
        {
            get => ReadInt(0x6C + Offset);
            set => Write(0x6C + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt70
        {
            get => ReadInt(0x70 + Offset);
            set => Write(0x70 + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt74
        {
            get => ReadInt(0x74 + Offset);
            set => Write(0x74 + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt78
        {
            get => ReadInt(0x78 + Offset);
            set => Write(0x78 + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt7C
        {
            get => ReadInt(0x7C + Offset);
            set => Write(0x7C + Offset, value);
        }

        [Category(categoryName)]
        public int UnknownInt80
        {
            get => ReadInt(0x80 + Offset);
            set => Write(0x80 + Offset, value);
        }

    }
}