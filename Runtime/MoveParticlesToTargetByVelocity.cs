using System;
using UnityEngine;

public class MoveParticlesToTargetByVelocity : MonoBehaviour
{
    [SerializeField] private ParticleSystem _system;

    [SerializeField] private Transform _target;

    [SerializeField] [Range(0f, 1f)] private float _elapsedMoveTime;

    private ParticleSystem.Particle[] _particles;

    private Transform Target
    {
        get => _target;
        set => _target = value;
    }

    private void Awake()
    {
        _particles = new ParticleSystem.Particle[_system.main.maxParticles];
    }

    public void Play(Transform target)
    {
        Target = target;
        _system.Play(true);
    }

    private void LateUpdate()
    {
        if (Target == null)
            return;

        Vector3 targetPos;
        switch (_system.main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                targetPos = _system.transform.InverseTransformPoint(Target.position);
                break;
            case ParticleSystemSimulationSpace.World:
                targetPos = Target.position;
                break;
            case ParticleSystemSimulationSpace.Custom:
                targetPos = _system.main.customSimulationSpace != null
                    ? _system.main.customSimulationSpace.InverseTransformPoint(Target.position)
                    : _system.transform.InverseTransformPoint(Target.position);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        int numParticlesAlive = _system.GetParticles(_particles);
        for (int i = 0; i < numParticlesAlive; i++)
        {
            var startLifetime = _particles[i].startLifetime;
            var remainingLifetime = _particles[i].remainingLifetime;
            var elapsedLifetime = startLifetime - remainingLifetime;
            var normalizedElapsedLifetime = elapsedLifetime / startLifetime;

            if (normalizedElapsedLifetime > _elapsedMoveTime)
            {
                Vector3 pos = _particles[i].position;
                Vector2 direction = targetPos - pos;
                float speed = direction.magnitude / remainingLifetime;

                _particles[i].velocity = direction.normalized * speed;
            }
        }

        _system.SetParticles(_particles, numParticlesAlive);
    }
}