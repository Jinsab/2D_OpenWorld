// RememberParticleSystem.cs
// Saves and restores runtime-changeable properties of Unity's ParticleSystem component
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using MemoryPack;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores runtime-changeable ParticleSystem properties.
	/// Handles main module, emission, shape, and other commonly modified settings.
	/// Optionally captures individual particle data for exact state restoration.
	/// </summary>
	[AddComponentMenu("Crystal Save/Unity/Remember Particle System")]
	[RequireComponent(typeof(ParticleSystem))]
	[RememberTarget(typeof(ParticleSystem))]
	[RememberIcon("ParticleSystem Icon")]
	public class RememberParticleSystem : SaveableComponent
	{
		[Header("Particle Data Capture")]
		[SerializeField]
		[Tooltip("If enabled, captures individual particle data (position, velocity, lifetime, color, size, rotation) for exact restoration. Increases save file size but provides perfect accuracy.")]
		private bool captureParticleData = false;

		[SerializeField]
		[Tooltip("Maximum number of particles to capture. Higher values increase accuracy but also save file size.")]
		private int maxParticlesToCapture = 1000;

		private ParticleSystem _particleSystem;
		private ParticleSystem.Particle[] _particleBuffer;

		protected override void Awake()
		{
			base.Awake();
			_particleSystem = GetComponent<ParticleSystem>();
		}

		protected override byte[] SerializeComponentData()
		{
			if (_particleSystem == null)
				return null;

			var data = new RememberParticleSystemData
			{
				// Main module
				Time = _particleSystem.time,
				IsPlaying = _particleSystem.isPlaying,
				IsPaused = _particleSystem.isPaused,
				IsStopped = _particleSystem.isStopped,
				IsEmitting = _particleSystem.isEmitting,
				ParticleCount = _particleSystem.particleCount,
				RandomSeed = _particleSystem.randomSeed,
				UseAutoRandomSeed = _particleSystem.useAutoRandomSeed,

				// Main module properties
				Duration = _particleSystem.main.duration,
				Loop = _particleSystem.main.loop,
				Prewarm = _particleSystem.main.prewarm,
				StartDelay = SerializeMinMaxCurve(_particleSystem.main.startDelay),
				StartDelayMultiplier = _particleSystem.main.startDelayMultiplier,
				StartLifetime = SerializeMinMaxCurve(_particleSystem.main.startLifetime),
				StartLifetimeMultiplier = _particleSystem.main.startLifetimeMultiplier,
				StartSpeed = SerializeMinMaxCurve(_particleSystem.main.startSpeed),
				StartSpeedMultiplier = _particleSystem.main.startSpeedMultiplier,
				StartSize = SerializeMinMaxCurve(_particleSystem.main.startSize),
				StartSizeMultiplier = _particleSystem.main.startSizeMultiplier,
				StartRotation = SerializeMinMaxCurve(_particleSystem.main.startRotation),
				StartRotationMultiplier = _particleSystem.main.startRotationMultiplier,
				StartColor = SerializeMinMaxGradient(_particleSystem.main.startColor),
				GravityModifier = SerializeMinMaxCurve(_particleSystem.main.gravityModifier),
				GravityModifierMultiplier = _particleSystem.main.gravityModifierMultiplier,
				SimulationSpeed = _particleSystem.main.simulationSpeed,
				SimulationSpace = (int)_particleSystem.main.simulationSpace,
				ScalingMode = (int)_particleSystem.main.scalingMode,
				PlayOnAwake = _particleSystem.main.playOnAwake,
				MaxParticles = _particleSystem.main.maxParticles,

				// Emission module
				EmissionEnabled = _particleSystem.emission.enabled,
				EmissionRateOverTime = SerializeMinMaxCurve(_particleSystem.emission.rateOverTime),
				EmissionRateOverTimeMultiplier = _particleSystem.emission.rateOverTimeMultiplier,
				EmissionRateOverDistance = SerializeMinMaxCurve(_particleSystem.emission.rateOverDistance),
				EmissionRateOverDistanceMultiplier = _particleSystem.emission.rateOverDistanceMultiplier,

				// Shape module
				ShapeEnabled = _particleSystem.shape.enabled,
				ShapeType = (int)_particleSystem.shape.shapeType,
				ShapeAngle = _particleSystem.shape.angle,
				ShapeRadius = _particleSystem.shape.radius,
				ShapeRadiusThickness = _particleSystem.shape.radiusThickness,
				ShapeArc = _particleSystem.shape.arc,
				ShapeArcMode = (int)_particleSystem.shape.arcMode,
				ShapeArcSpread = _particleSystem.shape.arcSpread,
				ShapeRotation = _particleSystem.shape.rotation,
				ShapeScale = _particleSystem.shape.scale,
				ShapeAlignToDirection = _particleSystem.shape.alignToDirection,
				ShapeRandomDirectionAmount = _particleSystem.shape.randomDirectionAmount,
				ShapeSphericalDirectionAmount = _particleSystem.shape.sphericalDirectionAmount,
				ShapeRandomPositionAmount = _particleSystem.shape.randomPositionAmount,

				// Velocity over Lifetime module
				VelocityOverLifetimeEnabled = _particleSystem.velocityOverLifetime.enabled,
				VelocityOverLifetimeSpace = (int)_particleSystem.velocityOverLifetime.space,

				// Limit Velocity over Lifetime module
				LimitVelocityOverLifetimeEnabled = _particleSystem.limitVelocityOverLifetime.enabled,
				LimitVelocityOverLifetimeDampen = _particleSystem.limitVelocityOverLifetime.dampen,

				// Force over Lifetime module
				ForceOverLifetimeEnabled = _particleSystem.forceOverLifetime.enabled,

				// Color over Lifetime module
				ColorOverLifetimeEnabled = _particleSystem.colorOverLifetime.enabled,

				// Color by Speed module
				ColorBySpeedEnabled = _particleSystem.colorBySpeed.enabled,

				// Size over Lifetime module
				SizeOverLifetimeEnabled = _particleSystem.sizeOverLifetime.enabled,

				// Size by Speed module
				SizeBySpeedEnabled = _particleSystem.sizeBySpeed.enabled,

				// Rotation over Lifetime module
				RotationOverLifetimeEnabled = _particleSystem.rotationOverLifetime.enabled,

				// Rotation by Speed module
				RotationBySpeedEnabled = _particleSystem.rotationBySpeed.enabled,

				// External Forces module
				ExternalForcesEnabled = _particleSystem.externalForces.enabled,
				ExternalForcesMultiplier = _particleSystem.externalForces.multiplier,

				// Noise module
				NoiseEnabled = _particleSystem.noise.enabled,

				// Collision module
				CollisionEnabled = _particleSystem.collision.enabled,

				// Triggers module
				TriggersEnabled = _particleSystem.trigger.enabled,

				// Sub Emitters module
				SubEmittersEnabled = _particleSystem.subEmitters.enabled,

				// Texture Sheet Animation module
				TextureSheetAnimationEnabled = _particleSystem.textureSheetAnimation.enabled,

				// Lights module
				LightsEnabled = _particleSystem.lights.enabled,

				// Trails module
				TrailsEnabled = _particleSystem.trails.enabled,

				// Custom Data module
				CustomDataEnabled = _particleSystem.customData.enabled,

				// Renderer
				RenderMode = (int)_particleSystem.GetComponent<ParticleSystemRenderer>()?.renderMode,
			};

			// Optionally capture individual particle data
			if (captureParticleData && _particleSystem.particleCount > 0)
			{
				int particleCount = Mathf.Min(_particleSystem.particleCount, maxParticlesToCapture);
				if (_particleBuffer == null || _particleBuffer.Length < particleCount)
					_particleBuffer = new ParticleSystem.Particle[particleCount];

				int actualCount = _particleSystem.GetParticles(_particleBuffer, particleCount);
				data.Particles = new ParticleData[actualCount];

				for (int i = 0; i < actualCount; i++)
				{
					data.Particles[i] = new ParticleData
					{
						Position = _particleBuffer[i].position,
						Velocity = _particleBuffer[i].velocity,
						StartLifetime = _particleBuffer[i].startLifetime,
						RemainingLifetime = _particleBuffer[i].remainingLifetime,
						StartSize = _particleBuffer[i].startSize,
						StartColor = _particleBuffer[i].startColor,
						Rotation = _particleBuffer[i].rotation,
						AngularVelocity = _particleBuffer[i].angularVelocity,
						AnimatedVelocity = _particleBuffer[i].animatedVelocity,
					};
				}
			}

			return MemoryPackSerializer.Serialize(data);
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			if (_particleSystem == null || data == null || data.Length == 0)
				return;

			var deserializedData = MemoryPackSerializer.Deserialize<RememberParticleSystemData>(data);
			if (deserializedData == null)
				return;

			// CRITICAL: Stop the ParticleSystem FIRST before modifying any properties
			// This prevents errors when setting duration, random seed, etc.
			_particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			// Main module
			var main = _particleSystem.main;
			main.duration = deserializedData.Duration;
			main.loop = deserializedData.Loop;
			main.prewarm = deserializedData.Prewarm;
			main.startDelay = DeserializeMinMaxCurve(deserializedData.StartDelay);
			main.startDelayMultiplier = deserializedData.StartDelayMultiplier;
			main.startLifetime = DeserializeMinMaxCurve(deserializedData.StartLifetime);
			main.startLifetimeMultiplier = deserializedData.StartLifetimeMultiplier;
			main.startSpeed = DeserializeMinMaxCurve(deserializedData.StartSpeed);
			main.startSpeedMultiplier = deserializedData.StartSpeedMultiplier;
			main.startSize = DeserializeMinMaxCurve(deserializedData.StartSize);
			main.startSizeMultiplier = deserializedData.StartSizeMultiplier;
			main.startRotation = DeserializeMinMaxCurve(deserializedData.StartRotation);
			main.startRotationMultiplier = deserializedData.StartRotationMultiplier;
			main.startColor = DeserializeMinMaxGradient(deserializedData.StartColor);
			main.gravityModifier = DeserializeMinMaxCurve(deserializedData.GravityModifier);
			main.gravityModifierMultiplier = deserializedData.GravityModifierMultiplier;
			main.simulationSpeed = deserializedData.SimulationSpeed;
			main.simulationSpace = (ParticleSystemSimulationSpace)deserializedData.SimulationSpace;
			main.scalingMode = (ParticleSystemScalingMode)deserializedData.ScalingMode;
			main.playOnAwake = deserializedData.PlayOnAwake;
			main.maxParticles = deserializedData.MaxParticles;

			// Emission module
			var emission = _particleSystem.emission;
			emission.enabled = deserializedData.EmissionEnabled;
			emission.rateOverTime = DeserializeMinMaxCurve(deserializedData.EmissionRateOverTime);
			emission.rateOverTimeMultiplier = deserializedData.EmissionRateOverTimeMultiplier;
			emission.rateOverDistance = DeserializeMinMaxCurve(deserializedData.EmissionRateOverDistance);
			emission.rateOverDistanceMultiplier = deserializedData.EmissionRateOverDistanceMultiplier;

			// Shape module
			var shape = _particleSystem.shape;
			shape.enabled = deserializedData.ShapeEnabled;
			shape.shapeType = (ParticleSystemShapeType)deserializedData.ShapeType;
			shape.angle = deserializedData.ShapeAngle;
			shape.radius = deserializedData.ShapeRadius;
			shape.radiusThickness = deserializedData.ShapeRadiusThickness;
			shape.arc = deserializedData.ShapeArc;
			shape.arcMode = (ParticleSystemShapeMultiModeValue)deserializedData.ShapeArcMode;
			shape.arcSpread = deserializedData.ShapeArcSpread;
			shape.rotation = deserializedData.ShapeRotation;
			shape.scale = deserializedData.ShapeScale;
			shape.alignToDirection = deserializedData.ShapeAlignToDirection;
			shape.randomDirectionAmount = deserializedData.ShapeRandomDirectionAmount;
			shape.sphericalDirectionAmount = deserializedData.ShapeSphericalDirectionAmount;
			shape.randomPositionAmount = deserializedData.ShapeRandomPositionAmount;

			// Velocity over Lifetime module
			var velocityOverLifetime = _particleSystem.velocityOverLifetime;
			velocityOverLifetime.enabled = deserializedData.VelocityOverLifetimeEnabled;
			velocityOverLifetime.space = (ParticleSystemSimulationSpace)deserializedData.VelocityOverLifetimeSpace;

			// Limit Velocity over Lifetime module
			var limitVelocity = _particleSystem.limitVelocityOverLifetime;
			limitVelocity.enabled = deserializedData.LimitVelocityOverLifetimeEnabled;
			limitVelocity.dampen = deserializedData.LimitVelocityOverLifetimeDampen;

			// Other modules (enabled states)
			var forceOverLifetime = _particleSystem.forceOverLifetime;
			forceOverLifetime.enabled = deserializedData.ForceOverLifetimeEnabled;
			
			var colorOverLifetime = _particleSystem.colorOverLifetime;
			colorOverLifetime.enabled = deserializedData.ColorOverLifetimeEnabled;
			
			var colorBySpeed = _particleSystem.colorBySpeed;
			colorBySpeed.enabled = deserializedData.ColorBySpeedEnabled;
			
			var sizeOverLifetime = _particleSystem.sizeOverLifetime;
			sizeOverLifetime.enabled = deserializedData.SizeOverLifetimeEnabled;
			
			var sizeBySpeed = _particleSystem.sizeBySpeed;
			sizeBySpeed.enabled = deserializedData.SizeBySpeedEnabled;
			
			var rotationOverLifetime = _particleSystem.rotationOverLifetime;
			rotationOverLifetime.enabled = deserializedData.RotationOverLifetimeEnabled;
			
			var rotationBySpeed = _particleSystem.rotationBySpeed;
			rotationBySpeed.enabled = deserializedData.RotationBySpeedEnabled;
			
			var noise = _particleSystem.noise;
			noise.enabled = deserializedData.NoiseEnabled;
			
			var collision = _particleSystem.collision;
			collision.enabled = deserializedData.CollisionEnabled;
			
			var trigger = _particleSystem.trigger;
			trigger.enabled = deserializedData.TriggersEnabled;
			
			var subEmitters = _particleSystem.subEmitters;
			subEmitters.enabled = deserializedData.SubEmittersEnabled;
			
			var textureSheetAnimation = _particleSystem.textureSheetAnimation;
			textureSheetAnimation.enabled = deserializedData.TextureSheetAnimationEnabled;
			
			var lights = _particleSystem.lights;
			lights.enabled = deserializedData.LightsEnabled;
			
			var trails = _particleSystem.trails;
			trails.enabled = deserializedData.TrailsEnabled;
			
			var customData = _particleSystem.customData;
			customData.enabled = deserializedData.CustomDataEnabled;

			// External Forces module
			var externalForces = _particleSystem.externalForces;
			externalForces.enabled = deserializedData.ExternalForcesEnabled;
			externalForces.multiplier = deserializedData.ExternalForcesMultiplier;

		// Restore random seed for deterministic playback
		_particleSystem.useAutoRandomSeed = deserializedData.UseAutoRandomSeed;
		if (!deserializedData.UseAutoRandomSeed)
			_particleSystem.randomSeed = deserializedData.RandomSeed;

		// CRITICAL: Use exact ParticleRewinder approach for frame-perfect scrubbing
		// Note: System was already stopped at the beginning of this method
		
		// Step 1: Reset to time 0 (CRITICAL - establishes clean state!)
		_particleSystem.Simulate(0f, true, true, false);
		
		// Step 3: Restore particle state (pixel-perfect OR deterministic)
		if (captureParticleData && deserializedData.Particles != null && deserializedData.Particles.Length > 0)
		{
			// Pixel-perfect restoration: Set individual particle positions/velocities
			if (_particleBuffer == null || _particleBuffer.Length < deserializedData.Particles.Length)
				_particleBuffer = new ParticleSystem.Particle[deserializedData.Particles.Length];

			for (int i = 0; i < deserializedData.Particles.Length; i++)
			{
				_particleBuffer[i].position = deserializedData.Particles[i].Position;
				_particleBuffer[i].velocity = deserializedData.Particles[i].Velocity;
				_particleBuffer[i].startLifetime = deserializedData.Particles[i].StartLifetime;
				_particleBuffer[i].remainingLifetime = deserializedData.Particles[i].RemainingLifetime;
				_particleBuffer[i].startSize = deserializedData.Particles[i].StartSize;
				_particleBuffer[i].startColor = deserializedData.Particles[i].StartColor;
				_particleBuffer[i].rotation = deserializedData.Particles[i].Rotation;
				_particleBuffer[i].angularVelocity = deserializedData.Particles[i].AngularVelocity;
				// Note: animatedVelocity is read-only, cannot be set directly
			}

			_particleSystem.SetParticles(_particleBuffer, deserializedData.Particles.Length);
		}
		else
		{
			// Deterministic restoration: Simulate to target time (ParticleRewinder method)
			// Since we already called Simulate(0f) above, this will deterministically
			// regenerate particles from seed and simulate forward to deserializedData.Time
			_particleSystem.Simulate(deserializedData.Time, true, true, false);
		}
		
		// Step 4: CRITICAL - Pause to freeze particles at this exact time
		// Without this, particles will continue simulating in Update()
		// This MUST be called after all Simulate() operations
		_particleSystem.Pause(true);

		// Note: We don't restore Play/Pause state because TimeMachine controls playback
		// The particle system should always be paused and controlled via Simulate()
	}

	#region Helper Methods for MinMaxCurve and MinMaxGradient

		private SerializedMinMaxCurve SerializeMinMaxCurve(ParticleSystem.MinMaxCurve curve)
		{
			return new SerializedMinMaxCurve
			{
				Mode = (int)curve.mode,
				Constant = curve.constant,
				ConstantMin = curve.constantMin,
				ConstantMax = curve.constantMax,
				CurveMultiplier = curve.curveMultiplier,
				// Note: Curves are not serialized for simplicity; extend if needed
			};
		}

		private ParticleSystem.MinMaxCurve DeserializeMinMaxCurve(SerializedMinMaxCurve data)
		{
			if (data == null)
				return new ParticleSystem.MinMaxCurve(0);

			var mode = (ParticleSystemCurveMode)data.Mode;
			switch (mode)
			{
				case ParticleSystemCurveMode.Constant:
					return new ParticleSystem.MinMaxCurve(data.Constant);
				case ParticleSystemCurveMode.TwoConstants:
					return new ParticleSystem.MinMaxCurve(data.ConstantMin, data.ConstantMax);
				default:
					return new ParticleSystem.MinMaxCurve(data.Constant);
			}
		}

		private SerializedMinMaxGradient SerializeMinMaxGradient(ParticleSystem.MinMaxGradient gradient)
		{
			return new SerializedMinMaxGradient
			{
				Mode = (int)gradient.mode,
				Color = gradient.color,
				ColorMin = gradient.colorMin,
				ColorMax = gradient.colorMax,
			};
		}

		private ParticleSystem.MinMaxGradient DeserializeMinMaxGradient(SerializedMinMaxGradient data)
		{
			if (data == null)
				return new ParticleSystem.MinMaxGradient(Color.white);

			var mode = (ParticleSystemGradientMode)data.Mode;
			switch (mode)
			{
				case ParticleSystemGradientMode.Color:
					return new ParticleSystem.MinMaxGradient(data.Color);
				case ParticleSystemGradientMode.TwoColors:
					return new ParticleSystem.MinMaxGradient(data.ColorMin, data.ColorMax);
				default:
					return new ParticleSystem.MinMaxGradient(data.Color);
			}
		}

		#endregion

		#region Public API for TimeMachine Integration

		/// <summary>
		/// Gets the current simulation speed of the particle system.
		/// Used by TimeMachine editor for restoring original playback speed.
		/// </summary>
		public float GetOriginalSimulationSpeed()
		{
			return _particleSystem != null ? _particleSystem.main.simulationSpeed : 1f;
		}

		#endregion
	}

	#region Data Structures

	[MemoryPackable]
	public partial class RememberParticleSystemData
	{
		// Playback state
		public float Time { get; set; }
		public bool IsPlaying { get; set; }
		public bool IsPaused { get; set; }
		public bool IsStopped { get; set; }
		public bool IsEmitting { get; set; }
		public int ParticleCount { get; set; }
		public uint RandomSeed { get; set; }
		public bool UseAutoRandomSeed { get; set; }

		// Main module
		public float Duration { get; set; }
		public bool Loop { get; set; }
		public bool Prewarm { get; set; }
		public SerializedMinMaxCurve StartDelay { get; set; }
		public float StartDelayMultiplier { get; set; }
		public SerializedMinMaxCurve StartLifetime { get; set; }
		public float StartLifetimeMultiplier { get; set; }
		public SerializedMinMaxCurve StartSpeed { get; set; }
		public float StartSpeedMultiplier { get; set; }
		public SerializedMinMaxCurve StartSize { get; set; }
		public float StartSizeMultiplier { get; set; }
		public SerializedMinMaxCurve StartRotation { get; set; }
		public float StartRotationMultiplier { get; set; }
		public SerializedMinMaxGradient StartColor { get; set; }
		public SerializedMinMaxCurve GravityModifier { get; set; }
		public float GravityModifierMultiplier { get; set; }
		public float SimulationSpeed { get; set; }
		public int SimulationSpace { get; set; }
		public int ScalingMode { get; set; }
		public bool PlayOnAwake { get; set; }
		public int MaxParticles { get; set; }

		// Emission module
		public bool EmissionEnabled { get; set; }
		public SerializedMinMaxCurve EmissionRateOverTime { get; set; }
		public float EmissionRateOverTimeMultiplier { get; set; }
		public SerializedMinMaxCurve EmissionRateOverDistance { get; set; }
		public float EmissionRateOverDistanceMultiplier { get; set; }

		// Shape module
		public bool ShapeEnabled { get; set; }
		public int ShapeType { get; set; }
		public float ShapeAngle { get; set; }
		public float ShapeRadius { get; set; }
		public float ShapeRadiusThickness { get; set; }
		public float ShapeArc { get; set; }
		public int ShapeArcMode { get; set; }
		public float ShapeArcSpread { get; set; }
		public Vector3 ShapeRotation { get; set; }
		public Vector3 ShapeScale { get; set; }
		public bool ShapeAlignToDirection { get; set; }
		public float ShapeRandomDirectionAmount { get; set; }
		public float ShapeSphericalDirectionAmount { get; set; }
		public float ShapeRandomPositionAmount { get; set; }

		// Velocity over Lifetime module
		public bool VelocityOverLifetimeEnabled { get; set; }
		public int VelocityOverLifetimeSpace { get; set; }

		// Limit Velocity over Lifetime module
		public bool LimitVelocityOverLifetimeEnabled { get; set; }
		public float LimitVelocityOverLifetimeDampen { get; set; }

		// Force over Lifetime module
		public bool ForceOverLifetimeEnabled { get; set; }

		// Color over Lifetime module
		public bool ColorOverLifetimeEnabled { get; set; }

		// Color by Speed module
		public bool ColorBySpeedEnabled { get; set; }

		// Size over Lifetime module
		public bool SizeOverLifetimeEnabled { get; set; }

		// Size by Speed module
		public bool SizeBySpeedEnabled { get; set; }

		// Rotation over Lifetime module
		public bool RotationOverLifetimeEnabled { get; set; }

		// Rotation by Speed module
		public bool RotationBySpeedEnabled { get; set; }

		// External Forces module
		public bool ExternalForcesEnabled { get; set; }
		public float ExternalForcesMultiplier { get; set; }

		// Noise module
		public bool NoiseEnabled { get; set; }

		// Collision module
		public bool CollisionEnabled { get; set; }

		// Triggers module
		public bool TriggersEnabled { get; set; }

		// Sub Emitters module
		public bool SubEmittersEnabled { get; set; }

		// Texture Sheet Animation module
		public bool TextureSheetAnimationEnabled { get; set; }

		// Lights module
		public bool LightsEnabled { get; set; }

		// Trails module
		public bool TrailsEnabled { get; set; }

		// Custom Data module
		public bool CustomDataEnabled { get; set; }

		// Renderer
		public int RenderMode { get; set; }

		// Individual particle data (optional)
		public ParticleData[] Particles { get; set; }
	}

	[MemoryPackable]
	public partial class SerializedMinMaxCurve
	{
		public int Mode { get; set; }
		public float Constant { get; set; }
		public float ConstantMin { get; set; }
		public float ConstantMax { get; set; }
		public float CurveMultiplier { get; set; }
	}

	[MemoryPackable]
	public partial class SerializedMinMaxGradient
	{
		public int Mode { get; set; }
		public Color Color { get; set; }
		public Color ColorMin { get; set; }
		public Color ColorMax { get; set; }
	}

	[MemoryPackable]
	public partial class ParticleData
	{
		public Vector3 Position { get; set; }
		public Vector3 Velocity { get; set; }
		public float StartLifetime { get; set; }
		public float RemainingLifetime { get; set; }
		public float StartSize { get; set; }
		public Color32 StartColor { get; set; }
		public float Rotation { get; set; }
		public float AngularVelocity { get; set; }
		public Vector3 AnimatedVelocity { get; set; }
	}

	#endregion
}
#endif
