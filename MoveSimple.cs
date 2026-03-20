using UnityEngine;
using UnityEngine.InputSystem;

public class MoveSimple : MonoBehaviour
{
	[SerializeField]
	InputActionAsset m_Controls;

	[SerializeField]
	float m_SkinWidth = 0.01f;

	[SerializeField]
	float m_MoveSpeed = 4;

	[SerializeField]
	LayerMask m_LayerMask = ~0;

	InputAction m_WalkAction;

	void Awake()
	{
		InputActionMap map = m_Controls.FindActionMap("Player", true);
		map.Enable();

		m_WalkAction = map.FindAction("Move");

		Debug.Assert(m_WalkAction != null);
	}

	void FixedUpdate()
	{
		Vector2 moveDir = m_WalkAction.ReadValue<Vector2>();
		Vector3 velocity = new Vector3(moveDir.x, 0, moveDir.y) * m_MoveSpeed;

		DoMove(Time.fixedDeltaTime, transform.position, new Vector3(1, 1, 1), velocity, out Vector3 newPos, out _);

		transform.position = newPos;
	}

	void DoMove(float deltaTime, Vector3 inPosition, Vector3 size, Vector3 inVelocity, out Vector3 position, out Vector3 velocity)
	{
		position = inPosition;
		velocity = inVelocity;

		const int kMaxBumps = 8;
		int bumps = 0;
		while (deltaTime > 0 && bumps < kMaxBumps)
		{
			float inVelMag = velocity.magnitude;
			float dist = inVelMag * deltaTime;

			if (dist == 0)
			{
				velocity = Vector3.zero;
				return;
			}

			Vector3 direction = velocity / inVelMag;

			bool didHit = CastPlayer(position, size, velocity * deltaTime, m_LayerMask, out RaycastHit hitInfo);

			float ratio = hitInfo.distance / dist;
			deltaTime -= ratio * deltaTime;

			position += direction * hitInfo.distance;

			if (!didHit)
				break;

			velocity = Vector3.ProjectOnPlane(velocity, hitInfo.normal);

			++bumps;
		}
	}

	bool CastPlayer(Vector3 center, Vector3 size, Vector3 movement, LayerMask layerMask, float skinWidth, out RaycastHit hitInfo)
	{
		float dist = movement.magnitude;

		if (dist == 0)
		{
			hitInfo = default;
			return false;
		}

		Vector3 direction = movement / dist;

		bool hit = Physics.BoxCast(
			center,
			size / 2,
			direction,
			out hitInfo,
			Quaternion.identity,
			dist + skinWidth,
			layerMask,
			QueryTriggerInteraction.Ignore
		);

		if (hit && hitInfo.collider is MeshCollider collider && !collider.convex)
		{
			do
			{
				int iTri = hitInfo.triangleIndex * 3;
				int iv0 = collider.sharedMesh.triangles[iTri];
				int iv1 = collider.sharedMesh.triangles[iTri + 1];
				int iv2 = collider.sharedMesh.triangles[iTri + 2];

				Vector3 v0 = collider.sharedMesh.vertices[iv0];
				Vector3 v1 = collider.sharedMesh.vertices[iv1];
				Vector3 v2 = collider.sharedMesh.vertices[iv2];

				Vector3[] triWorld = {
					collider.transform.localToWorldMatrix.MultiplyPoint(v0),
					collider.transform.localToWorldMatrix.MultiplyPoint(v1),
					collider.transform.localToWorldMatrix.MultiplyPoint(v2)
				};

				Vector3[] boxPoints =
				{
					center + new Vector3(-size.x / 2, -size.y / 2, -size.z / 2),
					center + new Vector3(size.x / 2, -size.y / 2, -size.z / 2),
					center + new Vector3(size.x / 2, -size.y / 2, size.z / 2),
					center + new Vector3(-size.x / 2, -size.y / 2, size.z / 2),
					center + new Vector3(-size.x / 2, size.y / 2, -size.z / 2),
					center + new Vector3(size.x / 2, size.y / 2, -size.z / 2),
					center + new Vector3(size.x / 2, size.y / 2, size.z / 2),
					center + new Vector3(-size.x / 2, size.y / 2, size.z / 2)
				};

				// Check tri face collision
				Vector3 triNormal = Vector3.Cross(triWorld[1] - triWorld[0], triWorld[2] - triWorld[1]);
				{
					bool faceSeparating = true;

					float facePlaneDist = Vector3.Dot(triWorld[0], triNormal);

					for (int i = 0; i < boxPoints.Length; ++i)
					{
						float d = Vector3.Dot(triNormal, boxPoints[i]);
						if (d < facePlaneDist)
						{
							faceSeparating = false;
							break;
						}
					}

					if (faceSeparating)
					{
						hitInfo.normal = triNormal;
						break;
					}
				}

				// Check box face collision
				{
					Vector3[] boxFaces =
					{
						new(1, 0, 0),
						new(-1, 0, 0),
						new(0, 1, 0),
						new(0, -1, 0),
						new(0, 0, 1),
						new(0, 0, -1),
					};

					float[] boxFacePlanes =
					{
						boxPoints[1].x,
						-boxPoints[0].x,
						boxPoints[4].y,
						-boxPoints[0].y,
						boxPoints[2].z,
						-boxPoints[0].z,
					};

					bool faceSeparating = false;
					for (int x = 0; x < boxFaces.Length; ++x)
					{
						float facePlaneDist = boxFacePlanes[x];

						faceSeparating = true;
						for (int i = 0; i < 3; ++i)
						{
							float d = Vector3.Dot(triWorld[i], boxFaces[x]);
							if (d < facePlaneDist)
							{
								faceSeparating = false;
								break;
							}
						}

						if (faceSeparating)
						{
							hitInfo.normal = -boxFaces[x];
							break;
						}
					}

					if (faceSeparating)
						break;
				}

				// Check edge collision
				{
					int[] boxEdges =
					{
						0, 1,
						1, 2,
						2, 3,
						3, 0,

						0, 4,
						1, 5,
						2, 6,
						3, 7,

						4, 5,
						5, 6,
						6, 7,
						7, 4,
					};

					bool edgeSeparating = false;
					for (int a = 0; a < boxEdges.Length / 2 && !edgeSeparating; ++a)
					{
						int i = boxEdges[a * 2];
						int j = boxEdges[a * 2 + 1];

						Vector3 boxA = boxPoints[i];
						Vector3 boxB = boxPoints[j];

						Vector3 boxEdge = boxB - boxA;

						for (int c = 0; c < 3; ++c)
						{
							int ti = c;
							int tj = (c + 1) % 3;
							Vector3 triEdge = triWorld[tj] - triWorld[ti];

							Vector3 edgeNormal = Vector3.Cross(boxEdge, triEdge);

							float dc = Vector3.Dot(edgeNormal, center);
							float edgePlaneDist = Vector3.Dot(edgeNormal, boxPoints[i]);

							if (dc == 0 && edgePlaneDist == 0)
							{
								continue;
							}

							if (edgePlaneDist < dc)
							{
								edgeNormal = -edgeNormal;
								edgePlaneDist = -edgePlaneDist;
							}

							edgeSeparating = true;
							for (int x = 0; x < 3; ++x)
							{
								float d = Vector3.Dot(edgeNormal, triWorld[x]);
								if (d < edgePlaneDist)
								{
									edgeSeparating = false;
									break;
								}
							}

							if (edgeSeparating)
							{
								hitInfo.normal = -edgeNormal;
								break;
							}
						}
					}

					if (edgeSeparating)
						break;
				}

				Debug.LogWarning("Failed to find separating axis");
				Debug.Break();
			} while (false);
		}

		hitInfo.normal.Normalize();

		float nDot = -Vector3.Dot(hitInfo.normal, direction);
		float newDist = hitInfo.distance - Mathf.Abs(m_SkinWidth / nDot);
		if (newDist < 0) newDist = 0;

		if (!hit)
			newDist = dist;

		hitInfo.distance = newDist;

		return hit;
	}
}