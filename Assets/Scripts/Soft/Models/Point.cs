using Unity.Burst;
using Unity.Mathematics;

namespace Soft
{
	[BurstCompile]
	public struct Point
	{
		public float mass;
		public float2 Position;
		public float2 Velocity;
		public float2 Forces;//accumulated forces
	}
}