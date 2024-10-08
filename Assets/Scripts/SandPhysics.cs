﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization.Json;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace Scripts
{
	public class SandPhysics
	{
		public FallingSand World;

		public bool Running => _running;
		private bool _running = false;
		private NativeArray<JobHandle> _jobs;
		private Stopwatch _stopwatch;
		private NativeBitArray UpdatedThisTick;

		public long LastTickTime => _lastTickTime;
		private long _lastTickTime;
		public SandPhysics(FallingSand world)
		{
			_stopwatch = new Stopwatch();
			World = world;
			_jobs = new NativeArray<JobHandle>(world.Chunks.Count, Allocator.Persistent);
			//updated.
			UpdatedThisTick = new NativeBitArray(world.Width*world.Height,Allocator.Persistent);
		}

		public void StepPhysicsAll(bool tickAll = false)
		{
			_stopwatch.Restart();
			_running = true;
			//reset all update data, new tick!
			UpdatedThisTick.Clear();
			
			//todo: cache this into an array of lists.
			//divide all of the chunks into sections that will operate independently.
			//a,b,c
			//d,e,f
			//h,i,j

			//should run a, c, h, j; b, i; d, f; e
			//every third row and column, then +1,+1, then +2,+3
			
			//
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
									var c = World.Chunks[new int2(ci, cj)];
									if (c.NeedsUpdatePhysics || tickAll)
									{
										current.Add(c);
									}
								}
							}
						}
					}
				}
				
				//now run this third of the jobs at once.

				//we can do all of the chunks in current concurrently, after the previous have completed.
				//lets cache a dependency
				var deps = JobHandle.CombineDependencies(_jobs);

				foreach (var chunk in current)
				{
					var job = new SandPhysicsJob
					{
						ChunkOffsetX = chunk.OffsetX,
						ChunkOffsetY = chunk.OffsetY,
						ChunkHeight = chunk.Height,
						ChunkWidth = chunk.Width,
						WorldPixels = World.Pixels,
						WorldWidth = World.Width,

						ChunkID = chunk.ID,
						Updated= this.UpdatedThisTick
						//todo: cache this array
					};
					
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
			_stopwatch.Stop();
			_lastTickTime = _stopwatch.ElapsedMilliseconds;

			//we have to loop through all of them (or all the ones where updated is true, at least.)
			foreach (var chunk in World.Chunks.Values)
			{
				//todo: do this for the top, bottom, right, and left columns to see if we were updated by a neighbor and, use faster check for self for all the sleepy bois.
				//losing this optimization is what gains me fast entity lookup, so it's worth the annoyance here.
				bool updated = false;
				for (int i = 0; i < chunk.Height; i++)
				{
					//we should be able to read chunk-width stretches of our update data
					
					if (UpdatedThisTick.TestAny(((i + chunk.OffsetY) * World.Width) + chunk.OffsetX, chunk.Width))
					{
						updated = true;
						break;
					}
				}
				chunk.UpdatedPhysics(updated);
			}
		}

		public void Dispose()
		{
			_jobs.Dispose();
			UpdatedThisTick.Dispose();
		}
	}

	[BurstCompile]
	public struct SandPhysicsJob : IJob
	{
		//moine
		[NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
		public NativeBitArray Updated;

		[NativeDisableContainerSafetyRestriction,NativeDisableParallelForRestriction]
		public NativeArray<Pixel> WorldPixels;
		public int ChunkOffsetX;
		public int ChunkOffsetY;

		public int ChunkWidth;
		public int ChunkHeight;
		public int WorldWidth;
		public int ChunkID;

		private int _max;
		private int _total;
		
		//avoid constant allocation
		private bool flop;
		private Random random;
		public void Execute()
		{
			_max = WorldPixels.Length;
			random = new Random((uint)(ChunkID+1+ChunkOffsetX));
			for (int x = 0; x < ChunkWidth; x++)
			{
				for (int y = 0; y < ChunkHeight; y++)
				{
					int index = (WorldWidth*(ChunkOffsetY+y)) + ChunkOffsetX+x;
					if (Updated.TestNone(index))
					{
						switch (WorldPixels[index])
						{
							case Pixel.Sand:
								if (SwapIfMatch(index, x, y, 0, 1, Pixel.Empty)) continue;
								if (SwapIfMatch(index, x, y, 0, 1, Pixel.Water)) continue;
								if (SwapIfMatch(index, x, y, flop ? -1 : 1, 1, Pixel.Empty)) continue;
								if (SwapIfMatch(index, x, y, flop ? 1 : -1, 1, Pixel.Empty)) continue;
								continue;
							case Pixel.Water:
								if (SwapIfMatch(index, x, y, 0, 1, Pixel.Empty)) continue;
								//check x spaces to to the right until not empty or last.
								if (SwapIfMatch(index, x, y, flop ? -1 : 1, 0, Pixel.Empty)) continue;
								if (SwapIfMatch(index, x, y, flop ? 1 : -1, 0, Pixel.Empty)) continue;
								continue;
						}
					}
				}
			}
		}

		
		//todo: control the movement conditions without uneccesary repetative calls.
		private bool SwapIfMatch(int index, int x, int y, int dx, int dy, Pixel testPixel)
		{
			int next = (WorldWidth * (ChunkOffsetY+y+dy)) + (ChunkOffsetX+x+dx);
			flop = random.NextBool();
			//out of bounds
			
			if (next < 0 || next >= _max)
			{
				return false;
			}

			//now...
			if (WorldPixels[next] == testPixel)
			{
				
				Updated.Set(next,true);
				Updated.Set(index,true);
				//swap
				WorldPixels[next] = WorldPixels[index];
				WorldPixels[index] = testPixel;
				return true;
			}
			else
			{
				return false;
			}
			
		}
	}
}