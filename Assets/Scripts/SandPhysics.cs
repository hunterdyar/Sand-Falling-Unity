﻿using System.Collections.Generic;
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
			//updated.
			UpdatedThisTick = new NativeBitArray(world.Width*world.Height,Allocator.Persistent);
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
			
			
			UpdatedThisTick.Clear();
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
									if (c.NeedsUpdatePhysics)
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
						ChunkDataOffset = chunk.Offset,
						ChunkHeight = chunk.Height,
						ChunkWidth = chunk.Width,
						WorldWidth = World.Width,
						WorldHeight = World.Height,
						WorldPixels = World.Pixels,
						NumberChunksWide = World._chunksWide,
						NumberChunksTall = World._chunksTall,
						ChunkIndex = chunk.Index,
						ChunkID = chunk.ID,
						UpdatedThisTick = this.UpdatedThisTick
						//todo: cache this array
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
				// bool updated = UpdatedThisTick.TestAny(job.ChunkDataOffset, job.ChunkHeight * job.ChunkWidth);
				// World.Chunks[job.ChunkIndex].UpdatedPhysics(updated);
			}

			//we have to loop through all of them (or all the ones where updated is true, at least.)
			foreach (var chunk in World.Chunks.Values)
			{
				bool updated = UpdatedThisTick.TestAny(chunk.Offset, chunk.Width * chunk.Height);
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
		public NativeBitArray UpdatedThisTick;

		[NativeDisableContainerSafetyRestriction,NativeDisableParallelForRestriction]
		public NativeArray<Pixel> WorldPixels;
		public int ChunkDataOffset;
		public int WorldWidth;
		public int WorldHeight;
		public int NumberChunksWide;
		public int NumberChunksTall;
		public int ChunkWidth;
		public int ChunkHeight;
		public int2 ChunkIndex;
		public int ChunkID;
		private int _total;
		private int _offsetX;
		private int _offsetY;
		public void Execute()
		{
			// _offsetX = Index.x * ChunkWidth;
			// _offsetY = Index.y * ChunkHeight;
			// _total = ChunkWidth * ChunkHeight;
			//do the physics for a single chunk.
			for (int x = 0; x < ChunkWidth; x++)
			{
				for (int y = 0; y < ChunkHeight; y++)
				{
					int index = ChunkDataOffset+(ChunkWidth*y+x);
					if ( WorldPixels[index] == Pixel.Sand && !UpdatedThisTick.TestAny(index))// && Updated.TestNone(i)
					{
						if (MoveIfEmpty(index,x,y, 0, 1))
						{
							continue;
						}
						
						// try down left.
						 if (MoveIfEmpty(index,x,y, -1, 1))
						 {
						 	continue;
						 }
						
						 //try down right.
						 if (MoveIfEmpty(index, x,y,1, 1))
						 {
						 	continue;
						 }
					}
				}
			}
		}

		private bool MoveIfEmpty(int gi,int x, int y, int dx, int dy)
		{
			int ncid = ChunkID;
			var nx = x + dx;
			var ny = y + dy;
			int local = (ChunkWidth * ny + nx);
			int next = ChunkDataOffset + local;
			//check if we are out of bounds, and get new next-index from a different chunk.
			if (nx < 0 || nx >= ChunkWidth || ny < 0 || ny >= ChunkHeight)
			{ 
				int cdx = nx < 0 ? -1 : (nx >= ChunkWidth ? 1 : 0);//normalize to -1,1
				int cdy = ny < 0 ? -1 : (ny >= ChunkHeight ? 1 : 0); //normalize to -1,1

				//we can assert that cdx != 0 or cdy != 0 because dx and dy should not be both 0.
				var destIndexX = ChunkIndex.x + cdx;
				var destIndexY = ChunkIndex.y+cdy;

				//check against world edges
				if (
					destIndexX < 0
				    || destIndexX >= NumberChunksWide
				    || destIndexY < 0
				    || destIndexY >= NumberChunksTall
				    ) {
					return false;
				}
				
				//calculate new offset in the worldPixels Array.
				//in the init, we use this:
				// int id = (_chunksWide * y) + x;
				// int o = id * pixelChunkSize * pixelChunkSize;
				
				ncid = (NumberChunksWide * destIndexY) + destIndexX;
				int newOffset = ncid * ChunkWidth * ChunkHeight;
				
				//calculate the new x,y positions 
				if (nx < 0)
				{
					//left, we go to the max dx.
					nx = ChunkWidth -1+dx;
				}else if (nx >= ChunkWidth)
				{
					//right, go to 0. (1 to the right is the leftmost 0 col)
					nx = dx - 1;
				}

				if (ny < 0)
				{
					//up, move to new bottom pos
					ny = ChunkHeight + dy - 1;
				}
				else if (ny >= ChunkHeight)
				{
					//down, move to top row of next offset.
					ny = dy - 1;
				}

				local = ChunkWidth * ny + nx;
				next = newOffset+local;
			}

			//now...
			if (WorldPixels[next] == Pixel.Empty)
			{
				//not neccesary, because a for loop will never check i twice. Can use it for counts, for now, tho.
				// Updated.Set(i, true);
				//Updated.Set(next, true);
				//swap position.
				UpdatedThisTick.Set(next,true);
				UpdatedThisTick.Set(gi,true);
				WorldPixels[next] = Pixel.Sand;
				WorldPixels[gi] = Pixel.Empty;
				return true;
			}
			else
			{
				return false;
			}
			return false;
		}
	}
}