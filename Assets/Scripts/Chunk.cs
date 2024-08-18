using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Scripts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

public class Chunk
{
	public static Color Transparent = new Color(0, 0, 0, 0);

	public Texture2D Texture => _chunkTex;
	private Texture2D _chunkTex;
	
	public int Width => _width;
	private int _width;
	public int Offset=>_offset;
	private int _offset;
	private int _totalWidth;
	public int Height => _height;
	public VisualElement VisualElement { get; set; }

	public int2 Index;
	private int _height;
	private float4 _bg;
	private NativeArray<float> _rawTexture;

	public bool didUpdateThisFrame;
	public FallingSand World;
	public Chunk(FallingSand world,int offset, int w, int h, int indexX, int indexY)
	{
		this._offset = offset;
		this.World = world;
		_width = w;
		_height = h;
		this._totalWidth = world._chunksWide * w;
		int total = w * h;
		Index = new int2(indexX, indexY);
		_chunkTex = new Texture2D(w, h,TextureFormat.RGBAFloat,true);
		_chunkTex.filterMode = FilterMode.Point;
		
		_rawTexture = new NativeArray<float>(total*4,Allocator.Persistent);
		didUpdateThisFrame = true;
		var bg = Random.ColorHSV();
		_bg = new float4(bg.r, bg.g, bg.b, bg.a);
	}

	public Pixel GetPixel(int x, int y)
	{
		return World.Pixels[_width * (y+_width*Index.y) + (x+(_height*Index.x))];
	}
	public void SetDidUpdate()
	{
		didUpdateThisFrame = true;
	}
	
	public SetTextureDataJob GetTextureJob()
	{
		return new SetTextureDataJob()
		{
			Pixels = World.Pixels,
			RawTexture = _rawTexture,
			Offset = _offset,
			Width = _width,
			Background = _bg,
		};
	}

	public void AfterTextureJob()
	{
		if (didUpdateThisFrame)
		{
			_chunkTex.SetPixelData(_rawTexture, 0, 0);
			_chunkTex.Apply();
		}

		didUpdateThisFrame = false;

	}
	
	
	public void Dispose()
	{
		_rawTexture.Dispose();
	}
	
	[BurstCompile(CompileSynchronously = true)]
	public struct SetTextureDataJob : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<Pixel> Pixels;
		
		[NativeDisableParallelForRestriction]
		public NativeArray<float> RawTexture;

		public int TotalWidth;
		public int Width;
		public int Offset; 
		public float4 Background;
		public void Execute(int i)
		{
			int x = i % Width;
			int y = i / Width;
			// int y = (
			switch (Pixels[Offset+((Width * y) +x)])
			{
				case Pixel.Empty:
					RawTexture[i * 4] = Background.x;
					RawTexture[i * 4 + 1] = Background.y;
					RawTexture[i * 4 + 2] = Background.z;
					RawTexture[i * 4 + 3] = Background.w;
					break;
				case Pixel.Solid:
					RawTexture[i * 4] = 0;
					RawTexture[i * 4 + 1] = 0;
					RawTexture[i * 4 + 2] = 0;
					RawTexture[i * 4 + 3] = 1f;
					break;
				case Pixel.Sand:
					RawTexture[i * 4] = Color.yellow.r;
					RawTexture[i * 4 + 1] = Color.yellow.g;
					RawTexture[i * 4 + 2] = Color.yellow.b;
					RawTexture[i * 4 + 3] = Color.yellow.a;
					break;
			}
		}
	}

	public void UpdatedPhysics(bool resultDidUpdate)
	{
		didUpdateThisFrame = didUpdateThisFrame || resultDidUpdate;
	}
}
