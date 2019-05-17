﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static IndustrialPark.ConverterFunctions;

namespace IndustrialPark
{
    public class DynaEnemyNeptune : DynaPlaceableBase
    {
        public override string Note => "Version is always 4";

        public DynaEnemyNeptune() : base()
        {
            Unknown50 = 0;
            Unknown54 = 0;
            Unknown58 = 0;
            Unknown5C = 0;
            Unknown60 = 0;
            Unknown64 = 0;
        }

        public DynaEnemyNeptune(IEnumerable<byte> enumerable) : base (enumerable)
        {
            Unknown50 = Switch(BitConverter.ToUInt32(Data, 0x50));
            Unknown54 = Switch(BitConverter.ToUInt32(Data, 0x54));
            Unknown58 = Switch(BitConverter.ToUInt32(Data, 0x58));
            Unknown5C = Switch(BitConverter.ToUInt32(Data, 0x5C));
            Unknown60 = Switch(BitConverter.ToUInt32(Data, 0x60));
            Unknown64 = Switch(BitConverter.ToUInt32(Data, 0x64));

            CreateTransformMatrix();
        }
        
        public override bool HasReference(uint assetID)
        {
            if (Unknown50 == assetID)
                return true;
            if (Unknown54 == assetID)
                return true;
            if (Unknown58 == assetID)
                return true;
            if (Unknown5C == assetID)
                return true;
            if (Unknown60 == assetID)
                return true;
            if (Unknown64 == assetID)
                return true;

            return base.HasReference(assetID);
        }

        public override void Verify(ref List<string> result)
        {
            base.Verify(ref result);
            
            Asset.Verify(Unknown50, ref result);
            Asset.Verify(Unknown54, ref result);
            Asset.Verify(Unknown58, ref result);
            Asset.Verify(Unknown5C, ref result);
            Asset.Verify(Unknown60, ref result);
            Asset.Verify(Unknown64, ref result);
        }

        public override byte[] ToByteArray()
        {
            List<byte> list = base.ToByteArray().ToList();

            list.AddRange(BitConverter.GetBytes(Switch(Unknown50)));
            list.AddRange(BitConverter.GetBytes(Switch(Unknown54)));
            list.AddRange(BitConverter.GetBytes(Switch(Unknown58)));
            list.AddRange(BitConverter.GetBytes(Switch(Unknown5C)));
            list.AddRange(BitConverter.GetBytes(Switch(Unknown60)));
            list.AddRange(BitConverter.GetBytes(Switch(Unknown64)));

            return list.ToArray();
        }

        [Category("Enemy Neptune")]
        public EnemyNeptuneType Type
        {
            get => (EnemyNeptuneType)(uint)Model_AssetID;
            set => Model_AssetID = (uint)value;
        }

        [Category("Enemy Neptune")]
        public AssetID Unknown50 { get; set; }

        [Category("Enemy Neptune")]
        public AssetID Unknown54 { get; set; }

        [Category("Enemy Neptune")]
        public AssetID Unknown58 { get; set; }

        [Category("Enemy Neptune")]
        public AssetID Unknown5C { get; set; }

        [Category("Enemy Neptune")]
        public AssetID Unknown60 { get; set; }

        [Category("Enemy Neptune")]
        public AssetID Unknown64 { get; set; }
    }
}