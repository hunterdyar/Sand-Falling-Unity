using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Scripts
{
	public class SandPhysics
	{
		public FallingSand World;
		private readonly NativeList<PendingPixelAssignment> _pending = new NativeList<PendingPixelAssignment>();

		public bool Running => _running;
		private bool _running = false;
		private Stack<SandPhysicsJob> _requiringDeallocation = new Stack<SandPhysicsJob>();
		private NativeArray<JobHandle> _jobs;

		public SandPhysics(FallingSand world)
		{
			World = world;
			_jobs = new NativeArray<JobHandle>(world.Chunks.Count, Allocator.Persistent);
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
						Height = chunk.Height,
						Width = chunk.Width,
						Index = chunk.Index,
						Pixels = chunk.Pixels,
						ChunksWide = World._chunksWide,
						ChunksTall = World._chunksTall,
						Pending = new NativeList<PendingPixelAssignment>(Allocator.Persistent),
						Updated = new NativeBitArray(chunk.Width * chunk.Height, Allocator.TempJob)
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
				foreach (var pending in job.Pending)
				{
					World.ApplyChange(pending);
				}
				job.Pending.Clear();//not needed!?
				job.Updated.Dispose();
				job.Pending.Dispose();
			}
		}

		public void Dispose()
		{
			_jobs.Dispose();
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
		public NativeList<PendingPixelAssignment> Pending;
		public NativeBitArray Updated;
		
		//injected
		public NativeArray<Pixel> Pixels;
		public int Width;
		public int Height;
		public int2 Index;
		public int ChunksWide;
		public int ChunksTall;
		public void Execute()
		{
			int total = Width * Height;
			//do the physics for a chunk.
			for (int i = 0; i < Pixels.Length; i++)
			{
				if (Pixels[i] == Pixel.Sand && Updated.TestNone(i))
				{
					int x = i % Width;
					int y = i / Width;
					int below = (y - 1) * Width + x;
					if (below > 0 && Pixels[below] == Pixel.Empty)
					{
						//not neccesary, because a for loop will never check i twice. Can use it for counts, for now, tho.
						// Updated.Set(i, true);
						Updated.Set(below,true);
						//swap position.
						Pixels[i] = Pixel.Empty;
						Pixels[below] = Pixel.Sand;
						continue;
					}
					else
					{
						if (below <= 0 && Index.y < ChunksTall - 1)
						{
							Updated.Set(i, true);//we need to force the chunk to redraw, at least.
							Pending.Add(new PendingPixelAssignment(Index, x, Height - 1, Pixel.Empty,
								new int2(Index.x, Index.y + 1), x, 0, Pixel.Sand));
							continue;
						}
					}
				}
			}
		}
		
		//we generaly are assuming that x or y will be negative or over the max and go out of bounds.
		//we are also assuming that all chunks have the same width and height.
		public int2 LocalToWorld(int x, int y)
		{
			return new int2(y + Width * Index.y, x + Height * Index.y);
		}
	}
}