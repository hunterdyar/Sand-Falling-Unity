using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Soft
{
	public partial class SoftPhysicsWorld
	{
		private NativeArray<Point> _points;
		private NativeArray<Spring> _springs;

		private int MaxPoints = 1000;
		private int MaxSprings = 1000;
		private int NumPoints=0;
		private int NumSprings=0;

		public void Init()
		{
			_points = new NativeArray<Point>(MaxPoints, Allocator.Persistent);
			_springs = new NativeArray<Spring>(MaxSprings, Allocator.Persistent);
		}
		//1.create objects
		//2. Accumulate Forces
		//2-1 Graviry
		//2-2 Spring
		//2-3 Pressure
		//3. Draw

		public void ApplyGravity(float2 gravity)
		{
			for (int i = 0; i < _points.Length; i++)
			{
				
				var p = _points[i];
				p.Forces.x = 0;
				p.Forces.y = p.mass*gravity.y;//*(Pressure-FinalPressure>=0)
				_points[i] = p;
			}
		}

		public void ApplySprings()
		{
			for (int i = 0; i < NumSprings; i++)
			{
				//apply the springs
			}
		}

		

		public void Dispose()
		{
			_points.Dispose();
			_springs.Dispose();
		}

		private float Distance(int pointA, int pointB)
		{
			return math.distance(_points[pointA].Position, _points[pointB].Position);
		}
		private void AddSpring( int pointA, int pointB)
		{
			Spring s = new Spring()
			{
				PointA = pointA,
				PointB = pointB,
				Length = Distance(pointA, pointB)
			};
			_springs[NumSprings] = s;
			NumSprings++;
		}
	}
}