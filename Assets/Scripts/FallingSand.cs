using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts
{
	public class FallingSand : MonoBehaviour
	{
		//store the data
		public Dictionary<int2, Chunk> Chunks => _chunks;
		private Dictionary<int2, Chunk> _chunks;

		public int _chunksWide;
		public int _chunksTall;
		private int pixelChunkSize = 128;
		public int Width => _width;
		private int _width;
		public int Height => _height;
		private int _height;
		public NativeArray<Pixel> Pixels => _pixels;

		private NativeArray<Pixel> _pixels;
		
		//run physics
		private SandPhysics _physics;
		//render the output.
		private VisualElement _renderContainer;

		private void Awake()
		{
			var doc = GetComponent<UIDocument>();
			_renderContainer = doc.rootVisualElement;

		}

		private void Start()
		{
			_chunks = new Dictionary<int2, Chunk>();
			_pixels = new NativeArray<Pixel>(_chunksWide*pixelChunkSize*_chunksTall*pixelChunkSize, Allocator.Persistent);
			_width = _chunksWide*pixelChunkSize;
			_height = _chunksTall*pixelChunkSize;
			for (int x = 0; x < _chunksWide; x++)
			{
				for (int y = 0; y < _chunksTall; y++)
				{
					//set element properties
					VisualElement e = new VisualElement();
					e.style.width = new StyleLength(new Length(pixelChunkSize, LengthUnit.Pixel));
					e.style.height = new StyleLength(new Length(pixelChunkSize, LengthUnit.Pixel));
					e.style.left = new StyleLength(new Length(pixelChunkSize * x, LengthUnit.Pixel));
					e.style.top = new StyleLength(new Length(pixelChunkSize * y, LengthUnit.Pixel));
					e.style.position = new StyleEnum<Position>(Position.Absolute);
					//flip upside down. 00 is top left, but in texture space it's bottom left.
					e.style.scale = new StyleScale(new Vector2(1, -1));
					//create chunk
					// int o = _width*_width * y + _height * x;
					int id = (_chunksWide * y) + x;
					int o = id * pixelChunkSize * pixelChunkSize;
					if (o > _pixels.Length)
					{
						Debug.LogError("fuck");
					}
					var chunk = new Chunk(this, o,pixelChunkSize, pixelChunkSize,x,y);
					chunk.VisualElement = e;
					_chunks.Add(new int2(x, y),chunk);
					
					//apply texture as background image to chunk. Texture is a reference, so this will just update.
					e.style.backgroundImage = Background.FromTexture2D(chunk.Texture);
					_renderContainer.Add(e);
				}
			}

			_physics = new SandPhysics(this);

		}

		private void FixedUpdate()
		{
			//finish the last frame if it's not finished yet.
		}
		
		private void Update()
		{
			RunWorldPhysics();
			 if (Input.GetMouseButton(0))
			 {
			 	var x = Mathf.FloorToInt(Input.mousePosition.x);
			 	var y = Mathf.FloorToInt(Screen.height-Input.mousePosition.y);
			    x = Mathf.Clamp(x, 0, _width);
			    y = Mathf.Clamp(y, 0, _height);
			    var chunk = GetChunkFromPixel(x, y);
			    if (chunk != null)
			    {
				    int2 cp = new int2(Mathf.FloorToInt(x -(chunk.Index.x*pixelChunkSize)), Mathf.FloorToInt(y -(chunk.Index.y*pixelChunkSize)));
				    SetPixel(chunk.Offset+(cp.y*pixelChunkSize+cp.x), Pixel.Sand);
				    chunk.SetDidUpdate();
			    }
			 }

			 List<JobHandle> handles = new List<JobHandle>();
			 //setup the visual loops.
			 foreach (var chunk in _chunks.Values)
			 {
				 if (chunk.didUpdateThisFrame)
				 {
					 chunk.VisualElement.style.unityBackgroundImageTintColor = Color.white;
				 }
				 else
				 {
					 chunk.VisualElement.style.unityBackgroundImageTintColor = Color.gray;
				 }
				 
				 if (chunk.didUpdateThisFrame)
				 {
					 var j = chunk.GetTextureJob();
					 var handle = j.Schedule(pixelChunkSize*pixelChunkSize,256);
					 handles.Add(handle);
				 }
				 
			 }
			 
			 //wait for one to complete. The rest will complete in about the same time, we'll wait for them all.
			 //if nothing updates, handles will be empty
			 foreach (var handle in handles)
			 {
				 handle.Complete();
			 }

			 //this resets didupdateThisFrame to false.
			 foreach (var chunk in _chunks.Values)
			 {
				 chunk.AfterTextureJob();
			 }
		}

		private void SetPixel(int i, Pixel sand)
		{
			_pixels[i] = sand;
		}

		private Chunk GetChunkFromPixel(int x, int y)
		{
			int2 xc = new int2(Mathf.FloorToInt(x / pixelChunkSize),Mathf.FloorToInt(y/pixelChunkSize));
			return _chunks.GetValueOrDefault(xc);
		}

		private void RunWorldPhysics()
		{
			//do physics step on world.
			if (!_physics.Running)
			{
				
			}

			_physics.StepPhysicsAll();
			_physics.Complete();
		}

		private void OnDestroy()
		{
			_pixels.Dispose();
			foreach (var chunk in _chunks.Values)
			{
				chunk.Dispose();
			}
			_physics.Dispose();
		}
	}
}