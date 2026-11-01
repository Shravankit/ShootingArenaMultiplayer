using System;
using UnityEngine;

namespace MultiplayerShooting.Gameplay.Combat
{
    public class BulletTracer : MonoBehaviour
    {
        [SerializeField] private float speed = 250f;

        private Vector3 end;
        private Action<BulletTracer> onFinished;
        private bool finished;

        public void Launch(Vector3 start, Vector3 target, Action<BulletTracer> finishedCallback)
        {
            onFinished = finishedCallback;
            finished = false;
            end = target;

            transform.position = start;

            Vector3 d = target - start;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(d);

            // Clear any trail left over from this object's previous flight.
            foreach (TrailRenderer t in GetComponentsInChildren<TrailRenderer>())
                t.Clear();
        }

        private void Update()
        {
            if (finished) return;

            transform.position = Vector3.MoveTowards(transform.position, end, speed * Time.deltaTime);

            if ((transform.position - end).sqrMagnitude < 0.01f)
            {
                finished = true;
                onFinished?.Invoke(this); // return to the pool instead of Destroy
            }
        }
    }
}