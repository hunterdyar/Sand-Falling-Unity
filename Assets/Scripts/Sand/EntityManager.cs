using System.ComponentModel;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Scripts
{
	public class EntityManager
	{
		public FallingSand World;
		private JobHandle _handle;
		public EntityManager(FallingSand world)
		{
			World = world;
		}

		public void LemmingsTick()
		{
			var job = new ProcessLemming()
			{
				Lemmings = World.Lemmings,
				Pixels = World.Pixels,
				WorldWidth = World.Width,
				WorldHeight = World.Height,
				ChunkWidth = World.pixelChunkSize,
			};
			_handle = job.Schedule(World.Lemmings.Length, 64);
			
		}

		public void LateLemmingsTick()
		{
			_handle.Complete();
		}
		
		
		public struct ProcessLemming : IJobParallelFor
		{
			public NativeList<Lemming> Lemmings;
			[Unity.Collections.ReadOnly]
			public NativeArray<Pixel> Pixels;
			public int WorldHeight;
			public int WorldWidth;
			public int ChunkWidth;
			public void Execute(int index)
			{
				var lemming = Lemmings[index];
				//self = lowest of two pixels.
				
				//wait! This isn't how the data is packed into the pixels array! It's by chunks!
				//mother fucker!
				
				int selfX = lemming.PositionIndex % WorldWidth;
				int selfY = lemming.PositionIndex / WorldWidth;
				var currentBelow = Pixels[(selfY) * WorldWidth + selfX];
				var currentAbove = Pixels[(selfY-1) * WorldWidth + selfX];

				//we will start very fragile, I guess.
				if (currentBelow != Pixel.Empty || currentAbove != Pixel.Empty)
				{
					lemming.State = LemmingState.Dead;
					Lemmings[index] = lemming;
					return;//go next loop, we're done here!
				}

				//move forward in facing direction
				var forward = Pixels[(selfY) * WorldWidth + selfX+lemming.FacingX];
				//if spot in front of us is available, move into it
				
				//otherwise, turn around.
			}

			
		}

		public void Dispose()
		{
		}
	}
}