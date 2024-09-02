using System;
using UnityEngine;

namespace Soft
{
	public class SoftRunner : MonoBehaviour
	{
		private SoftPhysicsWorld _world;

		public void Awake()
		{
			_world = new SoftPhysicsWorld();
			_world.Init();
		}

		public void Start()
		{
			_world.CreateBall(16,3,0,0);
		}

		public void OnDestroy()
		{
			if (_world != null)
			{
				_world.Dispose();
			}
		}
	}
}