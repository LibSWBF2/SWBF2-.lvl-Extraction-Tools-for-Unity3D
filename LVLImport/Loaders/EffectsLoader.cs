using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Enums;
using LibSWBF2.Utils;

using LibSWBF2.Types;
using LibVector2 = LibSWBF2.Types.Vector2;
using LibVector3 = LibSWBF2.Types.Vector3;
using LibVector4 = LibSWBF2.Types.Vector4;

using UVector2 = UnityEngine.Vector2;
using UVector3 = UnityEngine.Vector3;
using UMaterial = UnityEngine.Material;




/*

SWBF2 EFFECTS CONUNDRA

- My best guess of Circle spawner behavior:
	- Starting velocity components are set to VelocityScale
		- VelocityScale variance is a misnomer, e.g. (1,1) does not yield interval (0, 2), but rather (1,2).
	- The fields of Circle indicate PROBABILITIES that the starting components have
	  negative or positive multipliers
		- e.g. -1,1 yields equal probability of neg or pos multiplier, but -1000,1 yields far higher probability of neg multiplier
		- TODO: are 0 multipliers possible e.g. (0,1)? ANSWER: NO
	- This is easily tested by setting the Spawner type to Circle in PE and extremifying the Spread (i.e. Circle in .fx files) values 
	- If all Circle values are zeroed, it seems the PositionY value is still interpreted as (0,1)... see above TODO	


- Does VelocityScale only apply to the spawned velocity or does it persist across transformation?

- Inheritance of velocity
	- VelocityScale in Spawner does not affect InheritedVelocity

- Billboards and Rotation
	- StartRotation: How does one field affect multiple axes?
		- [0,1) faces Y, rotates around X
		- [1,2) faces X, rotates around Y
		- [2,3) faces Y, rotates around Z
		- [3,4) faces Z, rotates around Z
		- [4,5) faces Y, rotates around Y
		- [5,6) faces X, rotates around X
	- StartRotation = (min,max) adhering to above schema
	- RotationVelocity also adheres to above schema



UNITY MAPPING ISSUES (BUILT-IN PARTICLE SYSTEM, VFX GRAPH IS YET UNEXPLORED)

- Circle spawners
	- No clear way to emulate probablistic nature of Circle spawner velocities
		- On way could be setting starting speeds to extreme values and 
		using velocityLimitOverLifetime to limit those values to VelocityScale ranges
		- There is probably some way to set the starting speeds (one time) manually via script, 
		if it exists and is efficient then one could easily emulate this
			- Particle Systems have jobs/Burst integration, perhaps that would be good enough
		
- Velocity Inheritance
	- velocityOverLifetime (vOL) and inheritVelocity (iV) modules conflict?
		- Does vOL overwrite iV? ANSWER: 
			- 


*/



















public class EffectsLoader : Loader {

    // From standard particle shader GUI source...
    public enum BlendMode
    {
        Opaque,
        Cutout,
        Fade,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
        Transparent, // Physically plausible transparency mode, implemented as alpha pre-multiply
        Additive,
        Subtractive,
        Modulate
    }


    public static bool UseHDRP = false;

    static bool DBG = false;



    public static EffectsLoader Instance { get; private set; } = null;
    static EffectsLoader()
    {
        Instance = new EffectsLoader();
    }



    public void ImportEffects(string[] names)
    {
        UVector3 spawnLoc = UVector3.zero;
        foreach (string name in names)
        {
            var o = ImportEffect(name);
            o.transform.localPosition = spawnLoc;
            spawnLoc.z += 5.0f;
        }
    }


    public GameObject ImportEffect(string name)
    {
        Config fx = container.FindConfig(EConfigType.Effect, name);

        if (fx == null)
        { 
            Debug.LogWarningFormat("Effect {0} not found among loaded levels...", name);
            return null;
        }

        GameObject psObj = null;

        try 
        {
            psObj = ImportEffect(fx);
        }
        finally 
        {
            if (psObj != null) { psObj.name = name; }
        }

        return psObj;
    }


    // Creates a root empty for the effect and attaches top-level emitters (see UnpackNestedEmitters)
    // to child gameobjects.
    GameObject ImportEffect(Config fxConfig)
    {
        GameObject fxObject = new GameObject(String.Format("0x{0:x}", fxConfig.Name));

        List<Field> emitters = UnpackNestedEmitters(fxConfig);

        if (emitters == null) return fxObject;

        foreach (Field emitter in emitters)
        {
            //Debug.LogFormat("Getting emitter: {0}", emitter.GetString());
            GameObject emitterObj = GetEmitter(emitter);
            if (emitterObj != null)
            {
                emitterObj.transform.parent = fxObject.transform;
            }
        }        

        return fxObject;
    }


    // Returns a new GameObject with the input emitter converted to a particle system and attached.
    private GameObject GetEmitter(Field emitter, int ParentMaxParticles = 1)
    {
        GameObject fxObject = new GameObject(emitter.GetString());

        ParticleSystem uEmitter = fxObject.AddComponent<ParticleSystem>();
        uEmitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        ParticleSystemRenderer psR = fxObject.GetComponent<ParticleSystemRenderer>();

        Scope scEmitter = emitter.Scope;
        
        Scope scTransformer = scEmitter.GetField("Transformer").Scope;
        Scope scSpawner = scEmitter.GetField("Spawner").Scope;
        Scope scGeometry = scEmitter.GetField("Geometry").Scope;

        var mainModule = uEmitter.main;
        mainModule.startSpeed = 0f;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
        mainModule.loop = false;



        /*
        EMISSION

        - Need to determine the max particles when MaxParticles is set to -1.  Combine this
        with ringBufferMode to avoid the default 1000 particle setup.
        */

        var em = uEmitter.emission;
        em.enabled = true;
        em.rateOverTime = 0;

        // No adaptation needed
        var StartDelay = scEmitter.GetVec2("StartDelay");
        if (StartDelay.Magnitude() > .0001f)
        {
        	mainModule.startDelay = new ParticleSystem.MinMaxCurve(StartDelay.X, StartDelay.Y);
        }

        var maxParticles = scEmitter.GetVec2("MaxParticles").Y;


        float repeatInterval = scEmitter.GetVec2("BurstDelay").Y;

        var burstRange = scEmitter.GetVec2("BurstCount");
        short minCount = (short) burstRange.X;
        short maxCount = (short) burstRange.Y;

        if (repeatInterval > 0f && repeatInterval < .01f)
        {
            // Unity doesn't seem to allow burst repeat intervals i < .01 seconds,
            // but it does let you emit 1/i particles every second.  At this rate
            // it's not likely a player could even tell the emission was constant
            // instead of in bursts. 
            em.rateOverTime = minCount * (1f / repeatInterval);
        }
        else 
        {
            if (repeatInterval < 0.00001f)
            {
                // TODO: Probably not right, will get this one sooner or later
                repeatInterval = scTransformer.GetFloat("LifeTime");
            }

            int cycleCount;

            // Needs to be used to avoid thousands of particles in some cases
            if (maxParticles > 0.0f)
            {
                mainModule.maxParticles = (int) maxParticles;

                cycleCount = (int) (maxParticles / burstRange.Y) + 1;
                //Debug.LogFormat("Effect {0} has max bursts {1}", emitter.GetString(), burst.cycleCount);
            }
            else 
            {
                cycleCount = 0;
            }

            var burst = new ParticleSystem.Burst(0f, minCount, maxCount, cycleCount, repeatInterval);
            em.SetBursts(new ParticleSystem.Burst[]{burst});
        }


        /*
        DURATION & MAX PARTICLES

        - dont need to add StartDelay
        - SWBF terminates system when MaxParticles lifetimes are exceeded, but Unity starts reusing particles when MaxParticles is exceeded.
          Therefore we need to use duration to terminate the system.
        - Need to figure out how emission + duration/maxParticles work in subemitter context
        */

        float lifeTime = scTransformer.GetFloat("LifeTime");
        mainModule.startLifetime = lifeTime;
        mainModule.duration = lifeTime;

        bool HasZeroMaxParticles = maxParticles < .001f && maxParticles > -.001f;


        int MaxParticlesNeeded = 1; 

        if (HasZeroMaxParticles)
        {
        	// Seems like this is only the case for parent emitters...
        	MaxParticlesNeeded = (int) burstRange.Y;

            mainModule.maxParticles = (int) burstRange.Y;
	        mainModule.duration = 0.0001f;
        }
        // Will need to set duration such that system ends when max particles lifetimes expire
        else if (maxParticles > 0.001f)
        {
        	MaxParticlesNeeded = (int) maxParticles;
        	//mainModule.maxParticles = (int) maxParticles;

        	if (repeatInterval == 0) repeatInterval = lifeTime;
        	float timeOfMaxParticles = ((maxParticles - burstRange.Y) / burstRange.Y) * repeatInterval + lifeTime;
        	mainModule.duration = timeOfMaxParticles;

        }
        // Setting loop isn't good enough for negative MaxParticles
        else
        {
        	mainModule.duration = float.PositiveInfinity;

        	// mainModule
        	if (repeatInterval == 0) repeatInterval = lifeTime;

        	MaxParticlesNeeded = (int) (burstRange.Y + burstRange.Y * (lifeTime / repeatInterval));
            //mainModule.maxParticles = (int) (burstRange.Y + burstRange.Y * (lifeTime / repeatInterval));
        }

        mainModule.maxParticles = MaxParticlesNeeded * ParentMaxParticles;



        /*
        VELOCITY
        */

        bool HasEmptySpawner = SpawnerIsEmpty(scSpawner);
        bool HasStartVelocity = HasStartingVelocity(scSpawner);

        bool HasVelTransformation = HasVelocityTransformation(scTransformer, HasStartVelocity);
        
        var velModule = uEmitter.velocityOverLifetime;

        if (!HasEmptySpawner || HasVelTransformation)
        {
            ParticleSystem.MinMaxCurve[] VelCurves = ExtractVelocityOverLifetimeCurves(scEmitter, out ParticleSystem.MinMaxCurve scaleCurveOut);
            velModule.enabled = true;
            velModule.x = VelCurves[0];
            velModule.y = VelCurves[1];
            velModule.z = VelCurves[2];
            velModule.speedModifier = scaleCurveOut; 

            // TODO: Velocity at spawn (ie Circle/Spread) is local, but velocity transformation is global!!!
            // Unless we find a way to avoid using velOverLifetime for starting velocity we'll have to remap the starting
            // velocities to global axes at play
            velModule.space = HasVelTransformation ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local; 
        }

        if (!HasVelTransformation)
        {
            mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        // Need to add a magnitude func to libvec classes
        var inheritVelRange = scSpawner.GetVec2("InheritVelocityFactor");
        var inheritVelModule = uEmitter.inheritVelocity;
        if (inheritVelRange.Magnitude() > .001f)
        {
            // This does work in toy examples, but in converted fx it doesn't play 
            // well with velocityOverLifetime...
            inheritVelModule.enabled = true;
            inheritVelModule.mode = ParticleSystemInheritVelocityMode.Initial;

            inheritVelModule.curve = new ParticleSystem.MinMaxCurve(inheritVelRange.X, inheritVelRange.Y);
        }


        /*
        SHAPE
        */

        var shapeModule = uEmitter.shape;
        shapeModule.enabled = false;

        UVector3 spreadScale, spreadPos;
        if (!HasEmptySpawner)
        {   
            shapeModule.enabled = true;

            // This is hard because circles are weighted spawners which saturate their resulting values
            if (scSpawner.GetField("Circle") != null)
            {
                SpreadToPositionAndScale(scSpawner, out spreadScale, out spreadPos);
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
                shapeModule.position = spreadPos;
                shapeModule.radius = GetCircleRadius(scSpawner);
            }
            // This is pretty easy to emulate
            else if (scSpawner.GetField("Spread") != null)
            {
                // Set position distribution from the Offset field
                SpreadToPositionAndScale(scSpawner, out spreadScale, out spreadPos);
                shapeModule.shapeType = ParticleSystemShapeType.Box;
                shapeModule.scale = spreadScale;
                shapeModule.position = spreadPos;
            }
            else 
            {
                // TODO: Vortex, Rotator, None?
                // Debug.LogWarningFormat("Effect {0} has unhandled spawner type!", emitter.GetString());
                return null;
            }
        }

        /*
        SCALE
        */

        if (HasScaleTransformation(scTransformer))
        {
            if (ScaleTransformationToCurve(scTransformer, scSpawner.GetVec3("Size").Z, out ParticleSystem.MinMaxCurve curveOut))
            {
                var scaleModule = uEmitter.sizeOverLifetime;
                scaleModule.enabled = true;
                scaleModule.size = curveOut;
            }            
        }
        else 
        {
            mainModule.startSize = scSpawner.GetVec3("Size").Z; 
        }


        /*
        COLOR
        Spawner color settings + color transformation if needed.
        */

        var startingColorGradient = GetSpawnerColorInterval(scSpawner, out bool IsRGB);
        if (HasColorTransformation(scTransformer))
        {
            var colModule = uEmitter.colorOverLifetime;
            colModule.enabled = true;
            colModule.color = ColorTransformationToGradient(scTransformer, startingColorGradient, IsRGB);            
        }
        else 
        {
            mainModule.startColor = startingColorGradient;
        }



        string geomType = scGeometry.GetString("Type");


        /*
        ROTATION
        initial rotation + rotation velocity via rotationOverLifeTime module if needed.

        I dont think rotation transformers exist despite being implied by PE...
        */

        if (geomType.Equals("BILLBOARD", StringComparison.OrdinalIgnoreCase) || 
        	geomType.Equals("SPARK", StringComparison.OrdinalIgnoreCase))
        {

            mainModule.startRotation3D = true;
            mainModule.startRotationX = new ParticleSystem.MinMaxCurve(0f,0f);
            mainModule.startRotationY = new ParticleSystem.MinMaxCurve(0f,0f);
            mainModule.startRotationZ = new ParticleSystem.MinMaxCurve(0f,0f);

            var startRotField = scSpawner.GetVec3("StartRotation");
            float curveMin = startRotField.Y;
            float curveMax = startRotField.Z;

            float angleMin, angleMax;

            bool InBetween(float val, float min, float max)
            {
            	return val >= min && val < max;
            }

            if (InBetween(curveMin, 0f, 1f))
            {
            	angleMin = 90f + curveMin * 360f;
            	angleMax = 90f + curveMax * 360f;
            	mainModule.startRotationX = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            }
            else if (InBetween(curveMin, 1f, 2f))
            {
            	angleMin = 90f + (curveMin - 1f) * 360f;
            	angleMax = 90f + (curveMax - 1f) * 360f;
            	mainModule.startRotationY = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            }
            else if (InBetween(curveMin, 2f, 3f))
            {
            	angleMin = 90f + (curveMin - 2f) * 360f;
            	angleMax = 90f + (curveMin - 2f) * 360f;
            	mainModule.startRotationX = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            	mainModule.startRotationY = new ParticleSystem.MinMaxCurve(0.0174533f * 90f, 0.0174533f * 90f);
            }
            else if (InBetween(curveMin, 3f, 4f))
            {
            	angleMin = (curveMin - 3f) * 360f;
            	angleMax = (curveMin - 3f) * 360f;
            	mainModule.startRotationZ = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            }
            else if (InBetween(curveMin, 4f, 5f))
            {
            	angleMin = (curveMin - 4f) * 360f;
            	angleMax = (curveMin - 4f) * 360f;
            	mainModule.startRotationX = new ParticleSystem.MinMaxCurve(0.0174533f * 90f, 0.0174533f * 90f);
            	mainModule.startRotationY = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            }
            else 
            {
            	angleMin = (curveMin - 5f) * 360f;
            	angleMax = (curveMin - 5f) * 360f;
            	mainModule.startRotationY = new ParticleSystem.MinMaxCurve(0.0174533f * 90f, 0.0174533f * 90f);
            	mainModule.startRotationZ = new ParticleSystem.MinMaxCurve(0.0174533f * angleMin, 0.0174533f * angleMax);
            }

            // We can probably do this safely, since rotational velocity would be weird for billboards in SWBF2
            var rotModuleBillboard = uEmitter.rotationOverLifetime;
            rotModuleBillboard.enabled = false;
        }
        else 
        {
	        mainModule.startRotation = AngleCurveFromVec(scSpawner.GetVec3("StartRotation"));
	        
	        LibVector3 rotVel = scSpawner.GetVec3("RotationVelocity");
	        if (rotVel.Magnitude() > .0001f)
	        {
	            var rotModule = uEmitter.rotationOverLifetime;
	            rotModule.enabled = true;
	            rotModule.z = AngleCurveFromVec(rotVel);            
	        }
        }





        /*
        GEOMETRY + TEXTURE + MATERIAL

        TODO: EMITTER almost working perfectly, velocity inheritance is still slightly off
        */

        Texture2D tex = TextureLoader.Instance.ImportTexture(scGeometry.GetString("Texture"));
        //psR.enabled = tex != null;
        
        UMaterial mat = null;

        bool IsStreak = false;


        if (geomType.Equals("EMITTER", StringComparison.OrdinalIgnoreCase))
        {
            var subEmitterModule = uEmitter.subEmitters;
            subEmitterModule.enabled = true;

            //Get subemitters...
            foreach (var subEmitter in UnpackNestedEmitters(scGeometry.GetField("ParticleEmitter")))
            {
                var emObj = GetEmitter(subEmitter, MaxParticlesNeeded);
                if (emObj != null)
                {
                    emObj.transform.parent = fxObject.transform;
                    
                    ParticleSystem emPs = emObj.GetComponent<ParticleSystem>();
                    subEmitterModule.AddSubEmitter(emPs, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
                }
            }
        }
        // Works in all cases I've seen
        else if (geomType.Equals("GEOMETRY", StringComparison.OrdinalIgnoreCase))
        {
            // Debug.LogFormat("EffectsLoader needs model: {0}", scGeometry.GetString("Model"));

            if (ModelLoader.Instance.GetMeshesAndMaterialsFromSegments(scGeometry.GetString("Model"), out List<Mesh> Meshes, out List<UMaterial> Mats))
            {
                psR.renderMode = ParticleSystemRenderMode.Mesh;
                psR.alignment = ParticleSystemRenderSpace.Local;
                psR.SetMeshes(Meshes.ToArray()); 
                psR.sharedMaterials = Mats.ToArray();               
            }
        }
        // Decent, sometimes fails to display
        else if (geomType.Equals("SPARK", StringComparison.OrdinalIgnoreCase))
        {
            psR.renderMode = ParticleSystemRenderMode.Stretch;
            psR.velocityScale = scGeometry.GetFloat("SparkLength");
        	psR.alignment = ParticleSystemRenderSpace.World;
        }
        // Haven't seen an example yet.  Strangely, com_sfx_ord_exp has an texture sheet anim
        // ("Sparks"), but in the munged file it is missing and replaced by a duplicate of the 
        // next emitter ("ASparks")
        else if (geomType.Equals("ANIMATED", StringComparison.OrdinalIgnoreCase))
        {
            var tsam = uEmitter.textureSheetAnimation;
            tsam.enabled = true;
            tsam.animation = ParticleSystemAnimationType.WholeSheet;
            tsam.mode = ParticleSystemAnimationMode.Grid;
            tsam.timeMode = ParticleSystemAnimationTimeMode.FPS;
            tsam.fps = 1f / scGeometry.GetFloat("TimePerFrame");
            
            float frameSize = scGeometry.GetFloat("FrameSize");

            tsam.numTilesX = (int) (tex.width / frameSize);
            tsam.numTilesY = (int) (tex.height / frameSize);
        }
        else if (geomType.Equals("BILLBOARD", StringComparison.OrdinalIgnoreCase))
        {
            psR.alignment = ParticleSystemRenderSpace.World;
        }  
        else if (geomType.Equals("STREAK", StringComparison.OrdinalIgnoreCase))
        {
        	// TODO: Needed to for tri-fighter missiles and contrails
            var trailsModule = uEmitter.trails;
            trailsModule.enabled = true;
            trailsModule.ratio = 1f;
            trailsModule.inheritParticleColor = true;
            IsStreak = true;

            inheritVelModule.enabled = true;
            inheritVelModule.mode = ParticleSystemInheritVelocityMode.Current;
            inheritVelModule.curve = 1f;

            mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
            velModule.space = ParticleSystemSimulationSpace.World;
        }     
        // PARTICLE    
        else 
        {
            psR.alignment = ParticleSystemRenderSpace.View;
        }

        if (geomType.Equals("EMITTER", StringComparison.OrdinalIgnoreCase) || (tex == null && !geomType.Equals("GEOMETRY", StringComparison.OrdinalIgnoreCase)))
        {
            psR.enabled = false;
        }
        else 
        {
            if (mat == null && !geomType.Equals("GEOMETRY", StringComparison.OrdinalIgnoreCase))
            {                
                // TODO: Blendmode BLUR
                if (!UseHDRP)
                {
                    mat = new UMaterial(
                        scGeometry.GetString("BlendMode").Equals("ADDITIVE", StringComparison.OrdinalIgnoreCase) ? 
                            Resources.Load<UMaterial>("effects/ParticleAdditive") :
                            Resources.Load<UMaterial>("effects/ParticleNormal")
                    );
                    mat.mainTexture = tex;               
                }
                else 
                {
                    mat = new UMaterial(
                        scGeometry.GetString("BlendMode").Equals("ADDITIVE", StringComparison.OrdinalIgnoreCase) ? 
                            Resources.Load<UMaterial>("effects/HDRPParticleAdditive") :
                            Resources.Load<UMaterial>("effects/HDRPParticleNormal")
                    );
                    var mainTexID = Shader.PropertyToID("Texture2D_23DD87FD");
                    mat.SetTexture(mainTexID, tex);                    
                }

                psR.sharedMaterial = mat;
                if (IsStreak)
                {
                    psR.trailMaterial = mat;
                }
            }
        }
    
        return fxObject;
    }
    


    // Get spawner's starting color interval
    private ParticleSystem.MinMaxGradient GetSpawnerColorInterval(Scope spawner, out bool IsRGB)
    {
        Color minCol, maxCol;

        // Test if provided as HSV or RGB...
        if (spawner.GetField("Hue") != null)
        {
            var hRange = spawner.GetVec3("Hue");
            var sRange = spawner.GetVec3("Saturation");
            var vRange = spawner.GetVec3("Value");

            minCol = Color.HSVToRGB(hRange.Y/255.0f, sRange.Y/255.0f, vRange.Y/255.0f);
            maxCol = Color.HSVToRGB(hRange.Z/255.0f, sRange.Z/255.0f, vRange.Z/255.0f);

            IsRGB = false;
        }
        else
        {
            var rRange = spawner.GetVec3("Red");
            var gRange = spawner.GetVec3("Green");
            var bRange = spawner.GetVec3("Blue");

            minCol = new Color(rRange.Y/255.0f, gRange.Y/255.0f, bRange.Y/255.0f);
            maxCol = new Color(rRange.Z/255.0f, gRange.Z/255.0f, bRange.Z/255.0f);

            IsRGB = true;
        }

        var aRange = spawner.GetVec3("Alpha");
        minCol.a = aRange.Y/255.0f;
        maxCol.a = aRange.Z/255.0f;

        return new ParticleSystem.MinMaxGradient(minCol, maxCol);
    }


    // Must adapt for circles...
    bool HasStartingVelocity(Scope scSpawner)
    {
    	var velScale = scSpawner.GetVec2("VelocityScale");
    	if (velScale.Magnitude() < .00001) return false;

    	Field velSpawn;
    	if (scSpawner.GetField("Circle", out velSpawn) || scSpawner.GetField("Spread", out velSpawn))
    	{
	        var velScope = velSpawn.Scope;
	        return !(velScope.GetVec2("PositionX").Magnitude() +
	        		 velScope.GetVec2("PositionY").Magnitude() +
	        		 velScope.GetVec2("PositionZ").Magnitude() < .00001f);  		
    	}

    	// Eventually need to handle vortices/rotators
        return false;
    }



    bool SpawnerIsEmpty(Scope scSpawner)
    {
        bool Vec2IsZero(LibVector2 Vec)
        {
            return Mathf.Sqrt(Vec.X * Vec.X + Vec.Y * Vec.Y) < .0001;
        }

        var scOffset = scSpawner.GetField("Offset").Scope;
        if (!Vec2IsZero(scOffset.GetVec2("PositionX")) ||
            !Vec2IsZero(scOffset.GetVec2("PositionY")) ||
            !Vec2IsZero(scOffset.GetVec2("PositionZ")))
        {
            return false;
        }


        var circle = scSpawner.GetField("Circle");
        var scCircle = circle == null ? null : circle.Scope;
        if (scCircle != null && 
            Vec2IsZero(scCircle.GetVec2("PositionX")) &&
            Vec2IsZero(scCircle.GetVec2("PositionY")) &&
            Vec2IsZero(scCircle.GetVec2("PositionZ")))
        {
            return true;
        }

        // Keep separate from circle for now since we might
        // be able to save the module if the spread has no variance.  
        var spread = scSpawner.GetField("Spread");
        var scSpread = spread == null ? null : spread.Scope;
        if (scSpread != null &&
            Vec2IsZero(scSpread.GetVec2("PositionX")) &&
            Vec2IsZero(scSpread.GetVec2("PositionY")) &&
            Vec2IsZero(scSpread.GetVec2("PositionZ")))
        {
            return true;
        }

        return false;
    }





    private float GetCircleRadius(Scope spawner)
    {
        //var vScale = spawner.GetVec2("VelocityScale");
        //return new ParticleSystem.MinMaxCurve(vScale.X, vScale.Y);
        //var range = spawner.GetField("Circle").Scope.GetVec2("PositionX");
        //return range.Y;

        var Offset = spawner.GetField("Offset");
        var XRange = Offset.Scope.GetVec2("PositionX");

        return (XRange.Y - XRange.X) / 2f;
    }



    // Get spawner's starting position properties as a box's scale + position
    private void SpreadToPositionAndScale(Scope spawner, out UVector3 scale, out UVector3 position)
    {
        Scope offsetScope = spawner.GetField("Offset").Scope;

        var intervalX = offsetScope.GetVec2("PositionX");
        var intervalY = offsetScope.GetVec2("PositionY");
        var intervalZ = offsetScope.GetVec2("PositionZ");

        float convScale = 1.0f;
        float scaleX = Math.Abs(intervalX.Y - intervalX.X) / convScale;
        float scaleY = Math.Abs(intervalY.Y - intervalY.X) / convScale;
        float scaleZ = Math.Abs(intervalZ.Y - intervalZ.X) / convScale;

        float posX = (intervalX.Y + intervalX.X) / 2.0f;
        float posY = (intervalY.Y + intervalY.X) / 2.0f;
        float posZ = (intervalZ.Y + intervalZ.X) / 2.0f;

        scale = new UVector3(scaleX, scaleY, scaleZ);
        position = new UVector3(posX, posY, posZ);
    }


    // Get spawner's starting velocity distribution as list of component intervals.  
    // The out param scaleCurve is the "VelocityScale" property 
    private List<ParticleSystem.MinMaxCurve> SpreadToVelocityIntervals(Scope spawner, out ParticleSystem.MinMaxCurve scaleCurve) 
    {
        //bool isCircle = false;
        Field velDis = spawner.GetField("Spread");
        if (velDis == null)
        {
            velDis = spawner.GetField("Circle");
            //isCircle = true;
        }

        Scope spreadScope;
        if (velDis != null)
        {
            spreadScope = velDis.Scope;
        }
        else
        {
            scaleCurve = new ParticleSystem.MinMaxCurve(0.0f);
            return new List<ParticleSystem.MinMaxCurve>();
        }

        var vX = spreadScope.GetVec2("PositionX");
        var vY = spreadScope.GetVec2("PositionY");
        var vZ = spreadScope.GetVec2("PositionZ");

        var velScale = spawner.GetVec2("VelocityScale");
        scaleCurve = new ParticleSystem.MinMaxCurve(velScale.X, velScale.Y);

        var curveX = new ParticleSystem.MinMaxCurve(vX.X,vX.Y);
        var curveY = new ParticleSystem.MinMaxCurve(vY.X,vY.Y);
        //if (isCircle)
        //{
        //    curveY = new ParticleSystem.MinMaxCurve(vX.X,vX.Y);
        //}
        var curveZ = new ParticleSystem.MinMaxCurve(vZ.X,vZ.Y);

        return new List<ParticleSystem.MinMaxCurve>(){curveX, curveY, curveZ};
    }


    // Fx files/configs/chunks have subsequent emitters as children.  This function just 
    // recursively unpacks and returns the emitters in a list.  If an emitter is a proper
    // child/subemitter, it will be in the "Geometry" scope, and not included in the returned list.
    private List<Field> UnpackNestedEmitters(Config fxConfig)
    {
        var emitter = fxConfig.GetField("ParticleEmitter");
        if (emitter != null)
        {
            return UnpackNestedEmitters(emitter);
        }
        return null;
    }


    // Careful: the passed emitter is included in the returned list!
    private List<Field> UnpackNestedEmitters(Field emitter)
    {
        List<Field> emitters = new List<Field>();

        Field curEmitter = emitter;

        while (curEmitter != null)
        {
            emitters.Add(curEmitter);

            Field nextEmitter = curEmitter.Scope.GetField("ParticleEmitter");
            if (nextEmitter != null)
            {
                curEmitter = nextEmitter;
            }
            else 
            {
                break;
            }
        }

        return emitters;
    }



    private ParticleSystem.MinMaxCurve CurveFromVec(LibVector2 minMax)
    {
        return new ParticleSystem.MinMaxCurve(minMax.X, minMax.Y);
    }

    private ParticleSystem.MinMaxCurve AngleCurveFromVec(LibVector3 minMax)
    {
        return new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * minMax.Y, Mathf.Deg2Rad * minMax.Z);
    }



    /*
    COLOR TRANSFORMATION
    */

    public bool HasColorTransformation(Scope transformerScope)
    {
        float TxLifeTime = transformerScope.GetFloat("LifeTime");

        var CurrStage = transformerScope.GetField("Color");
        var CurrStageTime = CurrStage.Scope.GetFloat("LifeTime");

        if (CurrStage.Scope.GetField("Reach") == null &&
            CurrStage.Scope.GetField("Move")  == null)
        {
            return false;
        }
        else 
        {
            return true;
        }
    }


    public ParticleSystem.MinMaxGradient ColorTransformationToGradient(Scope transformerScope, ParticleSystem.MinMaxGradient initialGrad, bool IsRGB = true)
    {
        Gradient gradMin = new Gradient();
        Gradient gradMax = new Gradient();

        List<GradientColorKey> gradMinColKeys = new List<GradientColorKey>();
        List<GradientColorKey> gradMaxColKeys = new List<GradientColorKey>();

        List<GradientAlphaKey> gradMinAlphaKeys = new List<GradientAlphaKey>();
        List<GradientAlphaKey> gradMaxAlphaKeys = new List<GradientAlphaKey>();

        Color minColLast = initialGrad.colorMin;
        Color maxColLast = initialGrad.colorMax;

        float minAlphaLast = minColLast.a;
        float maxAlphaLast = maxColLast.a;

        minColLast.a = 0f;
        maxColLast.a = 0f;


        float lifeTime = transformerScope.GetFloat("LifeTime");
        float timeIndex = 0.00001f;

        gradMinColKeys.Add(new GradientColorKey(minColLast, timeIndex));
        gradMaxColKeys.Add(new GradientColorKey(maxColLast, timeIndex));

        gradMinAlphaKeys.Add(new GradientAlphaKey(minAlphaLast, timeIndex));
        gradMaxAlphaKeys.Add(new GradientAlphaKey(maxAlphaLast, timeIndex));

        Field curKey = transformerScope.GetField("Color");
        while (curKey != null)
        {
            Scope scKey = curKey.Scope;

            timeIndex = scKey.GetFloat("LifeTime") / lifeTime;

            if (scKey.GetField("Move", out Field keyMove))
            {
                var c = keyMove.GetVec4();

	            Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 0.0f);
	            if (!IsRGB)
	            {
	            	col = Color.HSVToRGB(col.r, col.g, col.b);
	            }
	            col.a = 0f;

	            minColLast += col;
	            maxColLast += col; 

                minAlphaLast = Mathf.Clamp(minAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(maxAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
            }
            else if (scKey.GetField("Reach", out Field keyReach))
            {
                var c = keyReach.GetVec4(); 
                Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 1.0f);
                if (!IsRGB)
	            {
	            	col = Color.HSVToRGB(col.r, col.g, col.b);
	            }
	            col.a = 0f;

                minColLast = col;
                maxColLast = col;

                minAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
            }
            else if (scKey.GetField("Scale", out Field keyScale))
            {
                var scale = keyScale.GetFloat(); 

                minColLast *= scale;
                maxColLast *= scale;

                minAlphaLast = Mathf.Clamp(minAlphaLast *= scale, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(maxAlphaLast *= scale, 0.0f, 1.0f);
            }
            else 
            {	
                break;
            }

	        gradMinColKeys.Add(new GradientColorKey(minColLast, timeIndex));
	        gradMaxColKeys.Add(new GradientColorKey(maxColLast, timeIndex));

            gradMinAlphaKeys.Add(new GradientAlphaKey(minAlphaLast, timeIndex));
            gradMaxAlphaKeys.Add(new GradientAlphaKey(maxAlphaLast, timeIndex));

            curKey = scKey.GetField("Next");
        }

        gradMin.SetKeys(gradMinColKeys.ToArray(), gradMinAlphaKeys.ToArray());
        gradMax.SetKeys(gradMaxColKeys.ToArray(), gradMaxAlphaKeys.ToArray());

        return new ParticleSystem.MinMaxGradient(gradMin, gradMax);
    }




    /*
    SIZE TRANSFORMATION
    */

    public bool HasScaleTransformation(Scope transformerScope)
    {
        float TxLifeTime = transformerScope.GetFloat("LifeTime");

        var CurrStage = transformerScope.GetField("Size");
        var CurrStageTime = CurrStage.Scope.GetFloat("LifeTime");

        if (CurrStage.Scope.GetField("Scale") == null)
        {
            return false;
        }
        else 
        {
            return true;
        }
    }


    private bool ScaleTransformationToCurve(Scope transformerScope, float initialScale, out ParticleSystem.MinMaxCurve curveOut)
    {
        float lifeTime = transformerScope.GetFloat("LifeTime");

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, initialScale);

        Field curScaleKey = transformerScope.GetField("Size"); 
        
        while (curScaleKey != null && curScaleKey.Scope.GetField("Scale") != null)
        {
            curve.AddKey(curScaleKey.Scope.GetFloat("LifeTime") / lifeTime, initialScale * curScaleKey.Scope.GetFloat("Scale"));
            curScaleKey = curScaleKey.Scope.GetField("Next");
        }

        if (curve == null)
        {
            curveOut = new ParticleSystem.MinMaxCurve(1.0f);
            return false;
        }
        else
        {
            curveOut = new ParticleSystem.MinMaxCurve(1.0f, curve);
            return true;
        }
    }



    /*
    VELOCITY
    */

    public bool HasVelocityTransformation(Scope transformerScope, bool HasStartingVelocity)
    {
        float TxLifeTime = transformerScope.GetFloat("LifeTime");

        Field curVelKey = transformerScope.GetField("Position"); 

        while (curVelKey != null)
        {
            var CurrScope = curVelKey.Scope;

            if (CurrScope.GetVec3("Accelerate", out LibVector3 Accel))
            {
                if (Accel.Magnitude() > .001f) return true;  
	                        	
            }
            else if (CurrScope.GetFloat("Scale", out float VelScale))
            {
            	return true; //if (Mathf.Abs(VelScale) < .00001f && HasStartingVelocity
            }
            else if (CurrScope.GetVec3("Reach", out LibVector3 ReachVel))
            {
            	return true;
            }

            curVelKey = CurrScope.GetField("Next");
        }

        return false;
    }


    public ParticleSystem.MinMaxCurve[] ExtractVelocityOverLifetimeCurves(Scope Emitter, out ParticleSystem.MinMaxCurve scaleCurve)
    {
        Scope spawner = Emitter.GetField("Spawner").Scope;

        Field velDis = spawner.GetField("Spread");

        bool isCircle = false;

        if (velDis == null)
        {
            velDis = spawner.GetField("Circle");
            isCircle = (velDis != null);
        }

        Scope spreadScope;
        if (velDis != null)
        {
            spreadScope = velDis.Scope;
        }
        else
        {
            scaleCurve = new ParticleSystem.MinMaxCurve(0.0f);
            return null;
        }

        var velScale = spawner.GetVec2("VelocityScale");
        //scaleCurve = new ParticleSystem.MinMaxCurve(1f);
        var scaleAnimCurve = new AnimationCurve();
        scaleAnimCurve.AddKey(0f,1f);

        // scaleCurve = new ParticleSystem.MinMaxCurve(velScale.X, velScale.Y);


        // This will eventually be optimized.  
        var animCurveMinX = new AnimationCurve();
        var animCurveMinY = new AnimationCurve();
        var animCurveMinZ = new AnimationCurve();


        var animCurveMaxX = new AnimationCurve();
        var animCurveMaxY = new AnimationCurve();
        var animCurveMaxZ = new AnimationCurve();


        var vX = spreadScope.GetVec2("PositionX");
        var vY = spreadScope.GetVec2("PositionY");
        var vZ = spreadScope.GetVec2("PositionZ");


        // Setting the timestamps to 0 doesn't seem to work, but a tiny epsilon gets the 
        // job done
        float prevXMin = 0f, prevXMax = 0f, prevYMin = 0f, prevYMax = 0f, prevZMin = 0f, prevZMax = 0f;

        float velScaleAvg = (velScale.X + velScale.Y) / 2f;

        if (isCircle)
        {
        	if (vX.Magnitude() < 0.0001f &&
        		vY.Magnitude() < 0.0001f &&
        		vZ.Magnitude() < 0.0001f)
			{
				prevYMin = velScale.X;
				prevYMax = velScale.Y;
			}
			else 
			{
				UVector3 spawnMults = new UVector3(MaxAbsoluteValue(vX), MaxAbsoluteValue(vY), MaxAbsoluteValue(vZ));
				spawnMults = spawnMults.normalized;

				float xMult = spawnMults.x; 
				float yMult = spawnMults.y; 
				float zMult = spawnMults.z;

	        	// X
	        	if (vX.X < -0.001f && vX.Y > .001f)
	        	{
	        		prevXMin = -velScaleAvg;
	        		prevXMax = velScaleAvg;
	        	}
	        	else if (vX.Y < -0.001f)
	        	{
	        		prevXMin = -velScale.X;
	        		prevXMax = -velScale.Y;
	        	}
	        	else if (vX.X > 0.001f)
	        	{
	        		prevXMin = velScale.X;
	        		prevXMax = velScale.Y;
	        	}

	        	// Y
	        	// If Y values are both zero, it actually seems to be interpreted as (0,1)...
	        	prevYMin = velScale.X;
	        	prevYMax = velScale.Y;

	        	if (vY.X < -0.001f && vY.Y > .001f)
	        	{
	        		prevYMin = -velScaleAvg;
	        		prevYMax = velScaleAvg;
	        	}
	        	else if (vY.Y < -0.001f)
	        	{
	        		prevYMin = -velScale.X;
	        		prevYMax = -velScale.Y;
	        	}
	        	else if (vY.X > 0.001f)
	        	{
	        		prevYMin = velScale.X;
	        		prevYMax = velScale.Y;
	        	}

	        	// Z
	        	if (vZ.X < -0.001f && vZ.Y > .001f)
	        	{
	        		prevZMin = -velScaleAvg;
	        		prevZMax = velScaleAvg;
	        	}
	        	else if (vZ.Y < -0.001f)
	        	{
	        		prevZMin = -velScale.X;
	        		prevZMax = -velScale.Y;
	        	}
	        	else if (vZ.X > 0.001f)
	        	{
	        		prevZMin = velScale.X;
	        		prevZMax = velScale.Y;
	        	}

	        	prevXMin *= xMult;				 
	        	prevXMax *= xMult;				 
	        	prevYMin *= yMult;				 
	        	prevYMax *= yMult;		        	
	        	prevZMin *= zMult;				 
	        	prevZMax *= zMult;	
			}
        }
        else 
        {
        	// X
        	if (vX.X < -0.001f && vX.Y > .001f)
        	{
        		prevXMin = vX.X * velScale.Y;
        		prevXMax = vX.Y * velScale.Y;
        	}
        	else 
        	{
        	    prevXMin = vX.X * velScale.X;
        		prevXMax = vX.Y * velScale.Y;	
        	}

        	// Y
        	if (vY.X < -0.001f && vY.Y > .001f)
        	{
        		prevYMin = vY.X * velScale.Y;
        		prevYMax = vY.Y * velScale.Y;
        	}
        	else 
        	{
        	    prevYMin = vY.X * velScale.X;
        		prevYMax = vY.Y * velScale.Y;	        		
        	}

        	// Z
        	if (vZ.X < -0.001f && vZ.Y > .001f)
        	{
        		prevZMin = vZ.X * velScale.Y;
        		prevZMax = vZ.Y * velScale.Y;
        	}
        	else
        	{
        		prevZMin = vZ.X * velScale.X;
        		prevZMax = vZ.Y * velScale.Y;
        	}

         	prevXMin = vX.X * velScale.X;
         	prevXMax = vX.Y * velScale.Y;
         	prevYMin = vY.X * velScale.X;
         	prevYMax = vY.Y * velScale.Y;
         	prevZMin = vZ.X * velScale.X;
         	prevZMax = vZ.Y * velScale.Y;
        }

        // Setting the time to 0 seemed not work iirc
	    animCurveMinX.AddKey(0.00001f, prevXMin);
	    animCurveMaxX.AddKey(0.00001f, prevXMax);
	    animCurveMinY.AddKey(0.00001f, prevYMin);
	    animCurveMaxY.AddKey(0.00001f, prevYMax);
	    animCurveMinZ.AddKey(0.00001f, prevZMin);
	    animCurveMaxZ.AddKey(0.00001f, prevZMax);


        Scope transformerScope = Emitter.GetField("Transformer").Scope;
        Field curVelKey = transformerScope.GetField("Position"); 


        float EmitterLifetime = transformerScope.GetFloat("LifeTime");
        float currXMin = 0f, currXMax = 0f, currYMin = 0f, currYMax = 0f, currZMin = 0f, currZMax = 0f;

        while (curVelKey != null)
        {
            var CurrScope = curVelKey.Scope;

            float TimeStamp = CurrScope.GetFloat("LifeTime") / EmitterLifetime;


            if (CurrScope.GetVec3("Accelerate", out LibVector3 Accel))
            {
                scaleAnimCurve.AddKey(TimeStamp, 1f);

	            if (Mathf.Abs(Accel.X) > .001f)
	            {
	            	currXMin = prevXMin + Accel.X;
	            	currXMax = prevXMax + Accel.X;
	                animCurveMinX.AddKey(TimeStamp, currXMin);
	                animCurveMaxX.AddKey(TimeStamp, currXMax);
	            }

	            if (Mathf.Abs(Accel.Y) > .001f)
	            {
	            	currYMin = prevYMin + Accel.Y;
	            	currYMax = prevYMax + Accel.Y;
	                animCurveMinY.AddKey(TimeStamp, currYMin);
	                animCurveMaxY.AddKey(TimeStamp, currYMax);      
	            }

	            if (Mathf.Abs(Accel.Z) > .001f)
	            {
	            	currZMin = prevZMin + Accel.Z;
	            	currZMax = prevZMax + Accel.Z;
	                animCurveMinZ.AddKey(TimeStamp, currZMin);
	                animCurveMaxZ.AddKey(TimeStamp, currZMax);   
	            }            	
            }
            else if (CurrScope.GetFloat("Scale", out float VelScale))
            {
            	scaleAnimCurve.AddKey(TimeStamp, VelScale);
            	
            	currXMin = prevXMin * VelScale;
            	currXMax = prevXMax * VelScale;
            	currYMin = prevYMin * VelScale;
            	currYMax = prevYMax * VelScale;
            	currZMin = prevZMin * VelScale;
            	currZMax = prevZMax * VelScale; 
            }
            else if (CurrScope.GetVec3("Reach", out LibVector3 ReachVel))
            {
            	currXMin = ReachVel.X;
            	currXMax = ReachVel.X;
            	currYMin = ReachVel.Y;
            	currYMax = ReachVel.Y;
            	currZMin = ReachVel.Z;
            	currZMax = ReachVel.Z;
	            
	            animCurveMinX.AddKey(TimeStamp, currXMin);
	            animCurveMaxX.AddKey(TimeStamp, currXMax);
                animCurveMinY.AddKey(TimeStamp, currYMin);
                animCurveMaxY.AddKey(TimeStamp, currYMax);      
                animCurveMinZ.AddKey(TimeStamp, currZMin);
                animCurveMaxZ.AddKey(TimeStamp, currZMax);   

                scaleAnimCurve.AddKey(TimeStamp, 1f);
            }
            else 
            {
            	// TODO: Are there other types?
            }

            prevXMin = currXMin;
            prevXMax = currXMax;
            prevYMin = currYMin;
            prevYMax = currYMax;
            prevZMin = currZMin;
            prevZMax = currZMax;
            
            curVelKey = curVelKey.Scope.GetField("Next");
        }

        var curveX = new ParticleSystem.MinMaxCurve(1f, animCurveMinX, animCurveMaxX);
        var curveY = new ParticleSystem.MinMaxCurve(1f, animCurveMinY, animCurveMaxY);
        var curveZ = new ParticleSystem.MinMaxCurve(1f, animCurveMinZ, animCurveMaxZ);


        scaleCurve = new ParticleSystem.MinMaxCurve(1f, scaleAnimCurve);
        return new ParticleSystem.MinMaxCurve[3] {curveX, curveY, curveZ};
    }


    float MaxAbsoluteValue(LibVector2 Vec)
    {
    	return Mathf.Max(Mathf.Abs(Vec.X), Mathf.Abs(Vec.Y));
    }
}
