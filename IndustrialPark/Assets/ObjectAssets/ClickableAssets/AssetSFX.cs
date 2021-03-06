﻿using HipHopFile;
using IndustrialPark.Models;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace IndustrialPark
{
    public class AssetSFX : BaseAsset, IRenderableAsset, IClickableAsset, IScalableAsset
    {
        private Matrix world;
        private Matrix world2;
        private BoundingBox boundingBox;

        public static bool dontRender = false;

        protected override int EventStartOffset => 0x30;

        public AssetSFX(Section_AHDR AHDR, Game game, Platform platform) : base(AHDR, game, platform)
        {
            _position = new Vector3(ReadFloat(0x1C), ReadFloat(0x20), ReadFloat(0x24));
            _radius = ReadFloat(0x28);
            _radius2 = ReadFloat(0x2C);

            CreateTransformMatrix();
            ArchiveEditorFunctions.renderableAssets.Add(this);
        }

        public override bool HasReference(uint assetID) => Sound_AssetID == assetID || base.HasReference(assetID);

        public override void Verify(ref List<string> result)
        {
            base.Verify(ref result);

            if (Sound_AssetID == 0)
                result.Add("SFX with Sound_AssetID set to 0");
            Verify(Sound_AssetID, ref result);
        }

        public void CreateTransformMatrix()
        {
            world = Matrix.Scaling(_radius * 2f) * Matrix.Translation(_position);
            world2 = Matrix.Scaling(_radius2 * 2f) * Matrix.Translation(_position);

            CreateBoundingBox();
        }

        public BoundingSphere boundingSphere;

        protected void CreateBoundingBox()
        {
            boundingSphere = new BoundingSphere(_position, _radius);
            boundingBox = BoundingBox.FromSphere(boundingSphere);
        }

        public float? GetIntersectionPosition(SharpRenderer renderer, Ray ray)
        {
            if (!ShouldDraw(renderer))
                return null;

            if (ray.Intersects(ref boundingSphere))
                return TriangleIntersection(ray, SharpRenderer.sphereTriangles, SharpRenderer.sphereVertices, world);
            return null;
        }
        
        public bool ShouldDraw(SharpRenderer renderer)
        {
            if (isSelected)
                return true;
            if (dontRender)
                return false;
            if (isInvisible)
                return false;

            if (AssetMODL.renderBasedOnLodt)
            {
                if (GetDistanceFrom(renderer.Camera.Position) < SharpRenderer.DefaultLODTDistance)
                    return renderer.frustum.Intersects(ref boundingBox);
                return false;
            }

            return renderer.frustum.Intersects(ref boundingBox);
        }

        public void Draw(SharpRenderer renderer)
        {
            renderer.DrawSphere(world, isSelected, renderer.sfxColor);

            if (isSelected)
                renderer.DrawSphere(world2, false, renderer.sfxColor);
        }

        [Browsable(false)]
        public bool SpecialBlendMode => true;

        public BoundingBox GetBoundingBox()
        {
            return boundingBox;
        }

        public float GetDistanceFrom(Vector3 cameraPosition)
        {
            return Vector3.Distance(cameraPosition, _position) - _radius;
        }

        [Category("Sound Effect")]
        public DynamicTypeDescriptor Flags08 => ByteFlagsDescriptor(0x8);
        [Category("Sound Effect")]
        public DynamicTypeDescriptor Flags09 => ByteFlagsDescriptor(0x9);

        [Category("Sound Effect")]
        public short Frequency
        {
            get => ReadShort(0xA);
            set => Write(0xA, value);
        }

        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float MinFrequency
        {
            get => ReadFloat(0xC);
            set => Write(0xC, value);
        }

        [Category("Sound Effect")]
        public AssetID Sound_AssetID
        {
            get => ReadUInt(0x10);
            set => Write(0x10, value);
        }

        [Category("Sound Effect")]
        public AssetID AttachAssetID
        {
            get => ReadUInt(0x14);
            set => Write(0x14, value);
        }

        [Category("Sound Effect")]
        public byte LoopCount
        {
            get => ReadByte(0x18);
            set => Write(0x18, value);
        }

        [Category("Sound Effect")]
        public byte Priority
        {
            get => ReadByte(0x19);
            set => Write(0x19, value);
        }

        [Category("Sound Effect")]
        public byte Volume
        {
            get => ReadByte(0x1A);
            set => Write(0x1A, value);
        }

        [Category("Sound Effect")]
        public byte Padding1B
        {
            get => ReadByte(0x1B);
            set => Write(0x1B, value);
        }

        private Vector3 _position;
        [Browsable(false)]
        public Vector3 Position => new Vector3(PositionX, PositionY, PositionZ);

        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float PositionX
        {
            get { return _position.X; }
            set
            {
                _position.X = value;
                Write(0x1C, _position.X);
                CreateTransformMatrix();
            }
        }

        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float PositionY
        {
            get { return _position.Y; }
            set
            {
                _position.Y = value;
                Write(0x20, _position.Y);
                CreateTransformMatrix();
            }
        }

        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float PositionZ
        {
            get { return _position.Z; }
            set
            {
                _position.Z = value;
                Write(0x24, _position.Z);
                CreateTransformMatrix();
            }
        }

        private float _radius;
        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float InnerRadius
        {
            get => _radius;
            set
            {
                _radius = value;
                Write(0x28, _radius);
                CreateTransformMatrix();
            }
        }

        private float _radius2;
        [Category("Sound Effect"), TypeConverter(typeof(FloatTypeConverter))]
        public float OuterRadius
        {
            get => _radius2;
            set
            {
                _radius2 = value;
                Write(0x2C, _radius2);
                CreateTransformMatrix();
            }
        }

        [Browsable(false)]
        public float ScaleX
        {
            get => InnerRadius;
            set
            {
                OuterRadius += value - InnerRadius;
                InnerRadius = value;
            }
        }
        [Browsable(false)]
        public float ScaleY
        {
            get => InnerRadius;
            set
            {
                OuterRadius += value - InnerRadius;
                InnerRadius = value;
            }
        }
        [Browsable(false)]
        public float ScaleZ
        {
            get => InnerRadius;
            set
            {
                OuterRadius += value - InnerRadius;
                InnerRadius = value;
            }
        }
    }
}