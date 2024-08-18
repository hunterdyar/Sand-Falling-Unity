using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Scripts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

public class Chunk
{
	public static Color Transparent = new Color(0, 0, 0, 0);

	public Texture2D Texture => _chunkTex;
	private Texture2D _chunkTex;
	public NativeArray<Pixel> Pixels => _pixels;
	private NativeArray<Pixel> _pixels;
	public int Width => _width;
	private int _width;
	public int Height => _height;

	public int2 Index;
	private int _height;
	private float4 _bg;
	private NativeArray<float> _rawTexture;

	public bool didUpdateThisFrame;
	public FallingSand World;
	public Chunk(FallingSand world,int w, int h, int indexX, int indexY)
	{
		this.World = world;
		_width = w;
		_height = h;
		int total = w * h;
		Index = new int2(indexX, indexY);
		_chunkTex = new Texture2D(w, h,TextureFormat.RGBAFloat,true);
		_chunkTex.filterMode = FilterMode.Point;
		
		_pixels = new NativeArray<Pixel>(total,Allocator.Persistent);
		_rawTexture = new NativeArray<float>(total*4,Allocator.Persistent);
		didUpdateThisFrame = true;
		var bg = Random.ColorHSV();
		_bg = new float4(bg.r, bg.g, bg.b, bg.a);
	}

	public Pixel GetPixel(int x, int y)
	{
		return _pixels[_width * (_height-y-1) + x];
	}
	public void SetPixel(int x, int y, Pixel pixel)
	{
		_pixels[_width * (_height-y-1) + x] = pixel;
		didUpdateThisFrame = true;
	}

	public Pixel GetPixelGlobal(int x, int y)
	{
		x = x - _width * Index.x;
		y = y - _height * Index.y;
		return _pixels[_width * (_height-y) + x];
	}

	//get or set a local when provided global pixel coordinates.
	public void SetPixelGlobal(int x, int y, Pixel pixel)
	{
		int i = _width * (_height - (y - _height * Index.y)-1) + (x - _width * Index.x);
		if (i >= 0 && i < _pixels.Length)
		{
			if (_pixels[i] != pixel)
			{
				_pixels[i] = pixel;
				didUpdateThisFrame = true;
			}
		}
		else
		{
			Debug.LogWarning("Tried to set global for bad pixel.");
		}
	}
	//this can be a multi-core job!
	public void ApplyTexture()
	{
		if (!didUpdateThisFrame)
		{
			//no need!
			return;
		}
		Debug.Log($"Updating Chunk {Index}");
		var setTexJob = new SetTextureDataJob()
		{
			Pixels = _pixels,
			RawTexture = _rawTexture,
			Background = _bg,
		};
		var handle = setTexJob.Schedule(_pixels.Length,128);
		
		//now wait for it to finish.
		handle.Complete();
		var compare = _chunkTex.GetPixelData<Color>(0);
		_chunkTex.SetPixelData(_rawTexture,0,0);
		//this one command transfers to GPU
		_chunkTex.Apply();
		didUpdateThisFrame = false;
	}
	
	public void Dispose()
	{
		_pixels.Dispose();
		_rawTexture.Dispose();
	}
	
	[BurstCompile(CompileSynchronously = true)]
	public struct SetTextureDataJob : IJobParallelFor
	{
		public NativeArray<Pixel> Pixels;
		[NativeDisableParallelForRestriction]
		public NativeArray<float> RawTexture;

		public float4 Background;
		public void Execute(int i)
		{
			switch (Pixels[i])
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
