using UnityEngine;

public class PooledParticle : ObjectPoolBase
{
    [SerializeField] private ParticleSystem particle;

    public override void OnSpawn()
    {
        base.OnSpawn();

        if (particle != null)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play();
        }
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        if (particle != null && !particle.IsAlive())
            OnDispawn();
    }
}
