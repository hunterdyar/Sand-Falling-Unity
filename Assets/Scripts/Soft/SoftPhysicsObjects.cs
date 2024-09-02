using Unity.Mathematics;
using UnityEngine;

namespace Soft
{
	public partial class SoftPhysicsWorld 
	{
		public void CreateBall(int numPoints, float radius, float px, float py)
		{
			int first = _points.Length + 1; //start index
			for (int i = 0; i < numPoints; i++)
			{
				_points[i] = new Point();
			}

			for (int i = first; i < first + numPoints; i++)
			{
				var p = _points[i];
				float x = radius * Mathf.Sin((i - first) * Mathf.Deg2Rad / numPoints);
				float y = radius * Mathf.Cos((i - first) * Mathf.Deg2Rad / numPoints);

				p.Position = new float2(x + px, y + py);
				_points[i] = p;
			}

			for (int i = first+1; i < first + numPoints; i++)
			{
				AddSpring(i-1, i);
				// AddSpring(i - 1, i - 1, 1);
			}
		}
	}
}