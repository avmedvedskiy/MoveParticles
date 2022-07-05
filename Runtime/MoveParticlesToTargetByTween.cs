using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class MoveParticlesToTargetByTween : MonoBehaviour
{
    private enum EaseType
    {
        Ease,
        AnimationCurve
    }

    [SerializeField] private ParticleSystem _system;

    [SerializeField] private Transform _target;

    [SerializeField] [Range(0f, 1f)] private float _elapsedMoveTime;

    [SerializeField] private EaseType _easeType;

    [SerializeField] private Ease _ease;

    [SerializeField] private AnimationCurve _easeAnimCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);

    [SerializeField] private ParticleSystem _onHitParticleSystem;

    [SerializeField] [Range(0.0f, 1.0f)] private float _onHitParticleProbability;
    
    public UnityEvent onHitEvent;

    public UnityEvent onComplete;


    private bool _isInitialized;
    private ParticleSystem.Particle[] _particles;

    private List<Tweener> _tweeners;
    private List<bool> _tweenerPlaying;
    private List<Vector3> _particlePositions;

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

    public void Play(Transform target,int count)
    {
        if(count <= 0)
            return;

        var burst = _system.emission.GetBurst(0);
        burst.count = count;
        _system.emission.SetBurst(0, burst);
        Play(target);
    }

    private void Awake()
    {
        _tweeners = new List<Tweener>();
        _tweenerPlaying = new List<bool>();
        _particlePositions = new List<Vector3>();
        _isInitialized = false;
    }

    private void OnDestroy()
    {
        CleanUp();
    }

    private void OnDisable()
    {
        CleanUp();
    }

    private void CleanUp()
    {
        if (_tweeners == null)
            return;

        foreach (var tweener in _tweeners)
            tweener.Kill(false);

        _tweeners.Clear();

        _tweenerPlaying.Clear();

        _particlePositions.Clear();

        _isInitialized = false;
    }

    private void Initialize()
    {
        if (_particles == null || _particles.Length < _system.main.maxParticles)
            _particles = new ParticleSystem.Particle[_system.main.maxParticles];

        _tweeners ??= new List<Tweener>();

        if (_system.particleCount == 0)
            return;

        for (int i = 0; i < _system.particleCount; i++)
        {
            _particlePositions.Add(Vector3.zero);
            Tweener tweener = null;
            tweener = DOTween.To(
                    () =>
                    {
                        var index = _tweeners.IndexOf(tweener);
                        return _particlePositions[index];
                    },
                    newPos =>
                    {
                        var index = _tweeners.IndexOf(tweener);
                        _particlePositions[index] = newPos;
                    },
                    Vector3.positiveInfinity,
                    float.MaxValue)
                .SetAutoKill(false)
                .Pause();

            tweener.OnComplete(() =>
            {
                var index = _tweeners.IndexOf(tweener);
                OnParticleHit(index);
            });


            _tweeners.Add(tweener);
            _tweenerPlaying.Add(false);
        }

        _isInitialized = true;
    }


    private void LateUpdate()
    {
        if (Target == null)
            return;

        if (_isInitialized == false)
            Initialize();

        // GetParticles is allocation free because we reuse the m_Particles buffer between updates
        int numParticlesAlive = _system.GetParticles(_particles);

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

        // Change only the particles that are alive
        for (int i = 0; i < _particlePositions.Count; i++)
        {
            var startLifetime = _particles[i].startLifetime;
            var remainingLifetime = _particles[i].remainingLifetime;
            var elapsedLifetime = startLifetime - remainingLifetime;
            var normalizedElapsedLifetime = elapsedLifetime / startLifetime;

            if (normalizedElapsedLifetime < _elapsedMoveTime)
            {
                _particlePositions[i] = _particles[i].position;
                _tweenerPlaying[i] = false;
                continue;
            }
            
            var currentPosition = _particlePositions[i];

            if (_tweenerPlaying[i])
            {
                _particles[i].velocity = Vector3.zero;
                _particles[i].position = currentPosition;
            }
            else
            {
                _tweenerPlaying[i] = true;

                var smoothTime = startLifetime - elapsedLifetime;
                _tweeners[i].ChangeStartValue(currentPosition, smoothTime);
                _tweeners[i].ChangeEndValue(targetPos);
                switch (_easeType)
                {
                    case EaseType.Ease:
                        _tweeners[i].SetEase(_ease);
                        break;
                    case EaseType.AnimationCurve:
                        _tweeners[i].SetEase(_easeAnimCurve);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _tweeners[i].Play();
            }
        }

        // Apply the particle changes to the Particle System
        _system.SetParticles(_particles, numParticlesAlive);
    }

    private void OnParticleHit(int index)
    {
        //Add particle hit

        _tweeners.RemoveAt(index);
        _tweenerPlaying.RemoveAt(index);
        _particlePositions.RemoveAt(index);

        onHitEvent.Invoke();

        if (_onHitParticleSystem != null)
        {
            bool spawn = UnityEngine.Random.Range(0.0f, 1.0f) < _onHitParticleProbability;
            if (spawn)
            {
                var diePs = Instantiate(_onHitParticleSystem, Target.transform.position, Quaternion.identity);
                diePs.Play(true);
                var main = diePs.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }
        }

        if (_tweeners.Count == 0)
        {
            _isInitialized = false;
            onComplete.Invoke();
        }
    }
}