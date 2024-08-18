using System;
using System.Collections.Generic;
using System.Linq;
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
					
					//create chunk
					var chunk = new Chunk(this, pixelChunkSize, pixelChunkSize,x,y);
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
			RunWorldPhysics();
			
		}

		private void LateUpdate()
		{
		}


		private void Update()
		{
			 if (Input.GetMouseButton(0))
			 {
			 	var x = Mathf.FloorToInt(Input.mousePosition.x);
			 	var y = Mathf.FloorToInt(Screen.height-Input.mousePosition.y);
			    x = Mathf.Clamp(x, 0, pixelChunkSize * _chunksWide);
			    y = Mathf.Clamp(y, 0, pixelChunkSize * _chunksTall);

			    var c = GetChunkFromPixel(x, y);
			    if (c != null)
			    {
				    c.SetPixelGlobal(x,y,Pixel.Sand);
			    }
			 }

			 foreach (var chunk in _chunks.Values)
			 {
				 chunk.ApplyTexture();
			 }
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
			foreach (var chunk in _chunks.Values)
			{
				chunk.Dispose();
			}
			_physics.Dispose();
		}

		public void ApplyChange(PendingPixelAssignment pending)
		{
			if (_chunks.TryGetValue(new int2(pending.attempting), out var chunk))
			{
				var p = chunk.GetPixel(pending.AX, pending.AY);
				if (p == Pixel.Empty)//todo: encode condition into the pending object.
				{
					chunk.SetPixel(pending.AX, pending.AY, pending.APixel);
					//if we could...
					if (_chunks.TryGetValue(new int2(pending.dependant), out var chunk2))
					{
						chunk2.SetPixel(pending.DX, pending.DY, pending.DPixel);
					}
				}
			}
		}
	}
}