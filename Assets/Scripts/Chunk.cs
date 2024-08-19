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
	public int OffsetX=>_offsetX;
	private readonly int _offsetX;
	public int OffsetY => _offsetY;
	private readonly int _offsetY;
	public int Height => _height;
	public bool NeedsUpdatePhysics = true;
	private bool _physicsUpdatedLastTick;
	public VisualElement VisualElement { get; set; }

	public int ID;
	public int2 Index;
	private int _height;
	private float4 _bg;
	private NativeArray<float> _rawTexture;
	public bool didUpdateThisFrame;
	public FallingSand World;

	/// <param name="id">unique id (index into chunks array)</param>
	/// <param name="w">width of chunk in pixels</param>
	/// <param name="h">height of chunk in pixels</param>
	/// <param name="indexX">chunk coordinate x</param>
	/// <param name="indexY">chunk coordinate y</param>
	public Chunk(FallingSand world, int id,int w, int h, int indexX, int indexY)
	{
		this.ID = id;
		this._offsetX = indexX * w;
		this._offsetY = indexY * h;
		this.World = world;
		_width = w;
		_height = h;
		int total = w * h;
		Index = new int2(indexX, indexY);
		_chunkTex = new Texture2D(w, h,TextureFormat.RGBAFloat,true);
		_chunkTex.filterMode = FilterMode.Point;
		
		_rawTexture = new NativeArray<float>(total*4,Allocator.Persistent);
		didUpdateThisFrame = true;
		var bg = new Color(0.1f, .1f, .1f, 1);
		_bg = new float4(bg.r, bg.g, bg.b, bg.a);
	}
	
	public void SetDidUpdate()
	{
		didUpdateThisFrame = true;//force to draw
		NeedsUpdatePhysics = true;//force to wake up physics
	}
	
	public SetTextureDataJob GetTextureJob()
	{
		return new SetTextureDataJob()
		{
			Pixels = World.Pixels,
			RawTexture = _rawTexture,
			OffsetX = _offsetX,
			OffsetY = _offsetY,
			Width = _width,
			TotalWidth = World.Width,
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
		public int OffsetX;
		public int OffsetY;
		public float4 Background;
		public void Execute(int i)
		{
			int x = i % Width;
			int y = i / Width;
			// int y = (
			switch (Pixels[(TotalWidth * (y+OffsetY)) + (x+OffsetX)])
			{
				case Pixel.Empty:
					RawTexture[i * 4] = Background.x;
					RawTexture[i * 4 + 1] = Background.y;
					RawTexture[i * 4 + 2] = Background.z;
					RawTexture[i * 4 + 3] = Background.w;
					break;
				case Pixel.Solid:
					RawTexture[i * 4] = 1;
					RawTexture[i * 4 + 1] = 1;
					RawTexture[i * 4 + 2] = 1;
					RawTexture[i * 4 + 3] = 1f;
					break;
				case Pixel.Sand:
					RawTexture[i * 4] = Color.yellow.r;
					RawTexture[i * 4 + 1] = Color.yellow.g;
					RawTexture[i * 4 + 2] = Color.yellow.b;
					RawTexture[i * 4 + 3] = 1f;
					break;
				case Pixel.Water:
					RawTexture[i * 4] = Color.blue.r;
					RawTexture[i * 4 + 1] = Color.blue.g;
					RawTexture[i * 4 + 2] = Color.blue.b;
					RawTexture[i * 4 + 3] = 1f;
					break;
			}
		}
	}

	public void UpdatedPhysics(bool resultDidUpdate)
	{
		//if we didn't change last frame and we didn't change this frame; go to sleep
		if (!didUpdateThisFrame && !resultDidUpdate && !_physicsUpdatedLastTick)
		{
			NeedsUpdatePhysics = false;
		}
		else
		{
			NeedsUpdatePhysics = true;
		}

		_physicsUpdatedLastTick = resultDidUpdate;
		
		didUpdateThisFrame = didUpdateThisFrame || resultDidUpdate;//cleared after rendering
	}
}
