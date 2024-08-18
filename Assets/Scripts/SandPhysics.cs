using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization.Json;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts
{
	public class SandPhysics
	{
		public FallingSand World;

		public bool Running => _running;
		private bool _running = false;
		private Stack<SandPhysicsJob> _requiringDeallocation = new Stack<SandPhysicsJob>();
		private NativeArray<JobHandle> _jobs;

		private NativeBitArray UpdatedThisTick;
		public SandPhysics(FallingSand world)
		{
			World = world;
			_jobs = new NativeArray<JobHandle>(world.Chunks.Count, Allocator.Persistent);
			UpdatedThisTick = new NativeBitArray(world.Width*world.Height, Allocator.Persistent);
		}

		public void StepPhysicsAll()
		{
			_running = true;
			//todo: cache this into an array of lists.
			//divide all of the chunks into sections that will operate independently.
			//a,b,c
			//d,e,f
			//h,i,j

			//should run a, c, h, j; b, i; d, f; e
			//every third row and column, then +1,+1, then +2,+3
			var chunks = World.Chunks;

			int jobIndex = 0;
			for (int xi = 0; xi < 3; xi++)
			{
				var current = new List<Chunk>();
				for (int ci = 0; ci < World._chunksWide; ci++)
				{
					if ((xi + ci) % 3 == 0)
					{
						//add every third row to this row.
						for (int yi = 0; yi < 3; yi++)
						{
							for (int cj = 0; cj < World._chunksTall; cj++)
							{
								if ((cj + yi) % 3 == 0)
								{
									current.Add(World.Chunks[new int2(ci, cj)]);
								}
							}
						}
					}
				}

				//we can do all of the chunks in current concurrently, after the previous have completed.
				//lets cache a dependency
				var deps = JobHandle.CombineDependencies(_jobs);

				foreach (var chunk in current)
				{
					var job = new SandPhysicsJob
					{
						ChunkDataOffset = chunk.Offset,
						ChunkHeight = chunk.Height,
						ChunkWidth = chunk.Width,
						WorldWidth = World.Width,
						WorldHeight = World.Height,
						Index = chunk.Index,
						//todo: chunk can save it's slice.
						Pixels = new NativeSlice<Pixel>(World.Pixels,chunk.Offset,chunk.Width*chunk.Height),
						//todo: cache this array
						Updated = new NativeBitArray(chunk.Width * chunk.Height, Allocator.Persistent)
					};
					
					_requiringDeallocation.Push(job);
					var handle = job.Schedule(deps);
					_jobs[jobIndex] = handle;
					jobIndex++;
				}
			}
			
			_running = false;
		}
		
		public void Complete()
		{
			//technically, we only need to call 'complete()' on the outermost jobs (hence the reversed loop)
			//but redundant calls are a harmless nop.
			for (int i = _jobs.Length - 1; i >= 0; i--)
			{
				_jobs[i].Complete();
			}

			while (_requiringDeallocation.TryPop(out var job))
			{
				bool updated = job.Updated.TestAny(0, job.Updated.Length);
				World.Chunks[job.Index].UpdatedPhysics(updated);
				//todo: can be cached to avoid the reallocationa
				job.Updated.Dispose();
			}
		}

		public void Dispose()
		{
			_jobs.Dispose();
			UpdatedThisTick.Dispose();
		}
	}

	[BurstCompile]
	public struct PendingPixelAssignment
	{
		//some enum for "Wants to Move Down"
		public int2 attempting;
		public int2 dependant;
		public int AX;//local
		public int AY;
		public int DX;
		public int DY;
		public Pixel APixel;
		public Pixel DPixel;

		public PendingPixelAssignment(int2 moveFrom, int fx, int fy, Pixel fp, int2 moveTo, int tx, int ty, Pixel tp)
		{
			dependant = moveFrom;
			DX = fx;
			DY = fy;
			DPixel = fp;
			attempting = moveTo;
			AX = tx;
			AY = ty;
			APixel = tp;
		}
	}

	[BurstCompile]
	public struct SandPhysicsJob : IJob
	{
		//moine
		public NativeBitArray Updated;
		
		//injected
		[NativeDisableContainerSafetyRestriction,NativeDisableParallelForRestriction]
		public NativeSlice<Pixel> Pixels;
		public int ChunkDataOffset;
		public int WorldWidth;
		public int WorldHeight;
		public int ChunkWidth;
		public int ChunkHeight;
		public int2 Index;
		
		private int _total;
		private int _offsetX;
		private int _offsetY;
		public void Execute()
		{
			// _offsetX = Index.x * ChunkWidth;
			// _offsetY = Index.y * ChunkHeight;
			// _total = ChunkWidth * ChunkHeight;
			//do the physics for a chunk.
			for (int x = 0; x < ChunkWidth; x++)
			{
				for (int y = 0; y < ChunkHeight; y++)
				{
					int i = (ChunkWidth*y+x);
					if (Pixels[i] == Pixel.Sand)// && Updated.TestNone(i)
					{
						if (MoveIfEmpty(i,x,y, 0, 1))
						{
							continue;
						}
						
						//try down left.
						if (MoveIfEmpty(i,x,y, -1, 1))
						{
							continue;
						}
						
						//try down right.
						if (MoveIfEmpty(i, x,y,1, 1))
						{
							continue;
						}
					}
				}
			}
		}

		private bool MoveIfEmpty(int i,int x, int y, int dx, int dy)
		{
			var nx = x + dx;
			var ny = y + dy;
			//check if we are in bounds, and thus only updating ourselves.
			if (nx > 0 && nx < ChunkWidth && ny > 0 && ny < ChunkHeight)
			{
				int next = (ChunkWidth * ny + nx);
				if (Pixels[next] == Pixel.Empty)
				{
					//not neccesary, because a for loop will never check i twice. Can use it for counts, for now, tho.
					// Updated.Set(i, true);
					//Updated.Set(next, true);
					//swap position.
					Pixels[next] = Pixels[i];
					Pixels[i] = Pixel.Empty;
					return true;
				}
				else
				{
					return false;
				}
			}
			//can't move out of bounds yet...
			return false;
		}
	}
}