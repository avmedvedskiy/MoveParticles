using System;
using UnityEngine;

[ExecuteAlways]
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

    public void Play(Transform target)
    {
        Target = target;
        _system.Play(true);
    }

    [SerializeField] private float lerpValue = 2f;
    [SerializeField] private float speedValue = 2f;

    private void LateUpdate()
    {
        if (Target == null)
            return;
        
        _particles ??= new ParticleSystem.Particle[_system.main.maxParticles];

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
                ref var particle = ref _particles[i];
                Vector3 pos = particle.position;
                Vector2 direction = targetPos - pos;
                
                //magic lerp and speed values
                float speed = direction.magnitude / remainingLifetime * speedValue;
                Vector3 newDirection = Vector3.Lerp(particle.velocity, direction.normalized * speed,
                    Time.deltaTime * lerpValue);

                _particles[i].velocity = newDirection;

                Debug.DrawRay(pos, newDirection.normalized, Color.red);
            }
        }

        _system.SetParticles(_particles, numParticlesAlive);
    }
}