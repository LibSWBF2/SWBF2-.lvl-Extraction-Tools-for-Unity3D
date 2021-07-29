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
    public GameObject ImportEffect(Config fxConfig)
    {
        GameObject fxObject = new GameObject(String.Format("0x{0:x}", fxConfig.Name));

        foreach (Field emitter in UnpackNestedEmitters(fxConfig))
        {
            GameObject emitterObj = GetEmitter(emitter);
            if (emitterObj != null)
            {
                emitterObj.transform.parent = fxObject.transform;
            }
        }        

        return fxObject;
    }


    // Returns a new GameObject with the input emitter converted to a particle system and attached.
    private GameObject GetEmitter(Field emitter)
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
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.0f);
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.loop = false;

        /*
        Handle bursts
        */

        float numParticles = scEmitter.GetVec2("MaxParticles").Y;

        var em = uEmitter.emission;
        em.enabled = true;
        em.rateOverTime = 0;

        var maxParticles = scEmitter.GetVec2("MaxParticles").Y;

        ParticleSystem.Burst burst = new ParticleSystem.Burst();
        float interval = scEmitter.GetVec2("BurstDelay").Y;
        if (interval < 0.00001f)
        {
            interval = scTransformer.GetFloat("LifeTime");
        }
        burst.repeatInterval = interval;
        var numRange = scEmitter.GetVec2("BurstCount");
        burst.minCount = (short) numRange.X;
        burst.maxCount = (short) numRange.Y;

        // Needs to be used to avoid thousands of particles in some cases
        if (maxParticles > 0.0f)
        {
            mainModule.maxParticles = (int) maxParticles;

            burst.cycleCount = (int) (maxParticles / numRange.Y);
            Debug.LogFormat("Effect {0} has max bursts {1}", emitter.GetString(), burst.cycleCount);
        }
        else 
        {
            burst.cycleCount = 0;
        }

        em.SetBursts(new ParticleSystem.Burst[]{burst});


        ParticleSystem.MinMaxCurve[] VelCurves = ExtractVelocityOverLifetimeCurves(scEmitter, out ParticleSystem.MinMaxCurve scaleCurveOut);
        var velModule = uEmitter.velocityOverLifetime;
        velModule.enabled = true;
        velModule.x = VelCurves[0];
        velModule.y = VelCurves[1];
        velModule.z = VelCurves[2];
        velModule.speedModifier = scaleCurveOut; 
        //velModule.space = ParticleSystemSimulationSpace.World;    


        // Set starting position distribution
        var shapeModule = uEmitter.shape;
        
        // This is hard because circles are weighted spawners which saturate their resulting values
        if (scSpawner.GetField("Circle") != null)
        {
            shapeModule.shapeType = ParticleSystemShapeType.Sphere;
            shapeModule.radius = GetCircleRadius(scSpawner);

            /*
            var curves = SpreadToVelocityIntervals(scSpawner, out ParticleSystem.MinMaxCurve scaleCurveOut);
            var velModule = uEmitter.velocityOverLifetime;
            velModule.enabled = true;
            velModule.x = curves[0];
            velModule.y = curves[1];
            velModule.z = curves[2];
            velModule.speedModifier = scaleCurveOut;
            */
        }
        // This is pretty easy to emulate
        else if (scSpawner.GetField("Spread") != null)
        {
            // Set position distribution from the Offset field
            SpreadToPositionAndScale(scSpawner, out UVector3 spreadScale, out UVector3 spreadPos);
            shapeModule.shapeType = ParticleSystemShapeType.Box;
            shapeModule.scale = spreadScale;
            shapeModule.position = spreadPos;

            // Set starting velocity distribution
            /*
            var curves = SpreadToVelocityIntervals(scSpawner, out ParticleSystem.MinMaxCurve scaleCurveOut);
            var velModule = uEmitter.velocityOverLifetime;
            velModule.enabled = true;
            velModule.x = curves[0];
            velModule.y = curves[1];
            velModule.z = curves[2];
            velModule.speedModifier = scaleCurveOut;
            */
        }
        else 
        {
            Debug.LogWarningFormat("Effect {0} has unhandled spawner type!", emitter.GetString());
            return null;
        }


        if (ScaleTransformationToCurve(scTransformer, scSpawner.GetVec3("Size").Z, out ParticleSystem.MinMaxCurve curveOut))
        {
            var scaleModule = uEmitter.sizeOverLifetime;
            scaleModule.enabled = true;
            scaleModule.size = curveOut;
        }



        // Set basic props in mainModule
        float lifeTime = scTransformer.GetFloat("LifeTime");
        mainModule.startLifetime = lifeTime;
        mainModule.duration = lifeTime;
        //mainModule.startSize = scSpawner.GetVec3("Size").Z;
        mainModule.startRotation = AngleCurveFromVec(scSpawner.GetVec3("StartRotation"));

        // Will eventually avoid enabling this when color doesn't change
        var colModule = uEmitter.colorOverLifetime;
        colModule.enabled = true;
        colModule.color = ColorTransformationToGradient(scTransformer, GetSpawnerColorInterval(scSpawner));

        // ^
        var rotModule = uEmitter.rotationOverLifetime;
        rotModule.enabled = true;
        rotModule.z = AngleCurveFromVec(scSpawner.GetVec3("RotationVelocity"));


        /*
        Geometry
        */

        string geomType = scGeometry.GetString("Type");

        Texture2D tex = null;
        UMaterial mat = null;
        if (geomType == "EMITTER")
        {
            var subEmitterModule = uEmitter.subEmitters;
            subEmitterModule.enabled = true;

            //Get subemitters...
            foreach (var subEmitter in UnpackNestedEmitters(scGeometry.GetField("ParticleEmitter")))
            {
                var emObj = GetEmitter(subEmitter);
                if (emObj != null)
                {
                    emObj.transform.parent = fxObject.transform;
                    ParticleSystem emPs = emObj.GetComponent<ParticleSystem>();
                    var emPsVelModule = emPs.velocityOverLifetime;
                    emPsVelModule.enabled = false;
                    var emPsMainModule = emPs.main;
                    shapeModule.radius = 0.0f;
                    mainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.0f);

                    subEmitterModule.AddSubEmitter(emPs, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
                }
            }
            tex = TextureLoader.Instance.ImportTexture(scGeometry.GetString("Texture"));
        }
        else if (geomType == "GEOMETRY")
        {
            Model model = container.Get<Model>(scGeometry.GetString("Model"));
            if (model == null)
            {
                Debug.LogWarningFormat("Failed to load model {0} used by emitter {1}", model.Name, emitter.GetString());
                return fxObject;
            }

            Mesh geomMesh = ModelLoader.Instance.GetFirstMesh(model);
            if (geomMesh != null)
            {
                geomMesh.name = model.Name;

                psR.renderMode = ParticleSystemRenderMode.Mesh;
                psR.SetMeshes(new Mesh[]{ geomMesh });
            }
            var mats = ModelLoader.Instance.GetNeededMaterials(model);
            if (mats.Count > 0)
            {
                mat = mats[0];
            }
        }
        else if (geomType == "SPARK")
        {
            psR.renderMode = ParticleSystemRenderMode.Stretch;
            psR.velocityScale = scGeometry.GetFloat("SparkLength");
            tex = TextureLoader.Instance.ImportTexture(scGeometry.GetString("Texture"));
        }
        else
        {
            // For now we handle billboards, and particles equivalently 
            tex = TextureLoader.Instance.ImportTexture(scGeometry.GetString("Texture"));
        }


        if (mat == null)
        {
            mat = new UMaterial(Shader.Find("Particles/Standard Unlit"));
            if (tex != null)
            {
                mat.mainTexture = tex;
            }
            else 
            {
                mat.color = new Color(0.0f,0.0f,0.0f,0.0f);
            }
        }


        // Need to find a way of doing this without triggering the annoying GUI reset!
        // Ideally without editing out the shader's GUI ref...
        string mode = scGeometry.GetString("BlendMode");
        if (mode == "ADDITIVE")
        {
            SetMaterialBlendMode(mat, BlendMode.Additive);
        }
        else 
        {
            SetMaterialBlendMode(mat, BlendMode.Fade);
        }

        psR.sharedMaterial = mat;

        return fxObject;
    }


    // Get spawner's starting color interval
    private ParticleSystem.MinMaxGradient GetSpawnerColorInterval(Scope spawner)
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
        }
        else
        {
            var rRange = spawner.GetVec3("Red");
            var gRange = spawner.GetVec3("Green");
            var bRange = spawner.GetVec3("Blue");

            minCol = new Color(rRange.Y/255.0f, gRange.Y/255.0f, bRange.Y/255.0f);
            maxCol = new Color(rRange.Z/255.0f, gRange.Z/255.0f, bRange.Z/255.0f);
        }

        var aRange = spawner.GetVec3("Alpha");
        minCol.a = aRange.Y/255.0f;
        maxCol.a = aRange.Z/255.0f;

        return new ParticleSystem.MinMaxGradient(minCol, maxCol);
    }



    private bool HandleCircle(ParticleSystem ps, Scope scSpawner)
    {
        return false;
        var circle = scSpawner.GetField("Circle");
        if (circle == null)
        {
            return false;
        }

        var velScale = scSpawner.GetVec2("VelocityScale"); 
        var posScale = scSpawner.GetVec2("PositionScale"); 

        var velModule = ps.velocityOverLifetime;
        velModule.enabled = true;

        var velLimitModule = ps.limitVelocityOverLifetime;
        velLimitModule.enabled = true;

        velLimitModule.limit = CurveFromVec(velScale);

        



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
        bool isCircle = false;
        Field velDis = spawner.GetField("Spread");
        if (velDis == null)
        {
            velDis = spawner.GetField("Circle");
            isCircle = true;
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



    public ParticleSystem.MinMaxGradient ColorTransformationToGradient(Scope transformerScope, ParticleSystem.MinMaxGradient initialGrad)
    {
        Gradient gradMin = new Gradient();
        Gradient gradMax = new Gradient();

        List<GradientColorKey> gradMinColKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> gradMinAlphaKeys = new List<GradientAlphaKey>();

        List<GradientColorKey> gradMaxColKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> gradMaxAlphaKeys = new List<GradientAlphaKey>();

        Color minColLast = initialGrad.colorMin;
        Color maxColLast = initialGrad.colorMax;

        float minAlphaLast = minColLast.a;
        float maxAlphaLast = maxColLast.a;


        float lifeTime = transformerScope.GetFloat("LifeTime");
        float timeIndex = 0.0f;

        gradMinColKeys.Add(new GradientColorKey(minColLast, timeIndex));
        gradMaxColKeys.Add(new GradientColorKey(maxColLast, timeIndex));

        gradMinAlphaKeys.Add(new GradientAlphaKey(minAlphaLast, timeIndex));
        gradMaxAlphaKeys.Add(new GradientAlphaKey(maxAlphaLast, timeIndex));

        Field curKey = transformerScope.GetField("Color");
        while (curKey != null)
        {
            Scope scKey = curKey.Scope;

            timeIndex = scKey.GetFloat("LifeTime") / lifeTime;

            Field keyMove, keyReach;
            if ((keyMove = scKey.GetField("Move")) != null)
            {
                var c = keyMove.GetVec4();
                Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 1.0f);

                minColLast += col;
                maxColLast += col;

                minAlphaLast = Mathf.Clamp(minAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(maxAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
            }
            else if ((keyReach = scKey.GetField("Reach")) != null)
            {
                var c = keyReach.GetVec4(); 
                Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 1.0f);

                minColLast = col;
                maxColLast = col;

                minAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
            }
            else 
            {
                return new ParticleSystem.MinMaxGradient(gradMin, gradMax);
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


    private bool ScaleTransformationToCurve(Scope transformerScope, float initialScale, out ParticleSystem.MinMaxCurve curveOut)
    {
        float lifeTime = transformerScope.GetFloat("LifeTime");
        float timeIndex = 0.0f;

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, initialScale);

        Field curScaleKey = transformerScope.GetField("Size"); 
        
        while (curScaleKey != null && curScaleKey.Scope.GetField("Scale") != null)
        {
            curve.AddKey(curScaleKey.Scope.GetFloat("LifeTime") / lifeTime, curScaleKey.Scope.GetFloat("Scale"));
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
    private ParticleSystem.MinMaxCurve[] PositionTransformationToCurve(Scope transformerScope)
    {
        ParticleSystem.MinMaxCurve[] Curves = new ParticleSystem.MinMaxCurve[3];


        float lifeTime = transformerScope.GetFloat("LifeTime");
        float timeIndex = 0.0f;

        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, initialVel);

        Field curScaleKey = transformerScope.GetField("Position"); 
        
        while (curScaleKey != null && curScaleKey.Scope.GetField("Accelerate") != null)
        {
            curve.AddKey(curScaleKey.Scope.GetFloat("LifeTime") / lifeTime, curScaleKey.Scope.GetVec3("Accelerate").);
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
    */




    public ParticleSystem.MinMaxCurve[] ExtractVelocityOverLifetimeCurves(Scope Emitter, out ParticleSystem.MinMaxCurve scaleCurve)
    {
        Scope spawner = Emitter.GetField("Spawner").Scope;

        Field velDis = spawner.GetField("Spread");
        if (velDis == null)
        {
            velDis = spawner.GetField("Circle");
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
        scaleCurve = new ParticleSystem.MinMaxCurve(velScale.X, velScale.Y);


        var animCurveMinX = new AnimationCurve();
        var animCurveMinY = new AnimationCurve();
        var animCurveMinZ = new AnimationCurve();


        var animCurveMaxX = new AnimationCurve();
        var animCurveMaxY = new AnimationCurve();
        var animCurveMaxZ = new AnimationCurve();


        var vX = spreadScope.GetVec2("PositionX");
        var vY = spreadScope.GetVec2("PositionY");
        var vZ = spreadScope.GetVec2("PositionZ");

        animCurveMinX.AddKey(0f, vX.X);
        animCurveMaxX.AddKey(0f, vX.Y);

        animCurveMinY.AddKey(0f, vY.X);
        animCurveMaxY.AddKey(0f, vY.Y);

        animCurveMinZ.AddKey(0f, vZ.X);
        animCurveMaxZ.AddKey(0f, vZ.Y);


        Scope transformerScope = Emitter.GetField("Transformer").Scope;
        Field curScaleKey = transformerScope.GetField("Position"); 
        
        while (curScaleKey != null && curScaleKey.Scope.GetField("Accelerate") != null)
        {
            var Accel = curScaleKey.Scope.GetVec3("Accelerate");
            float TimeStamp = curScaleKey.Scope.GetFloat("LifeTime");
            if (Mathf.Abs(Accel.X) > .001f)
            {
                animCurveMinX.AddKey(TimeStamp, vX.X + Accel.X);
                animCurveMaxY.AddKey(TimeStamp, vX.Y + Accel.X);
            }

            if (Mathf.Abs(Accel.Y) > .001f)
            {
                animCurveMinX.AddKey(TimeStamp, vY.X + Accel.Y);
                animCurveMaxY.AddKey(TimeStamp, vY.Y + Accel.Y);      
            }

            if (Mathf.Abs(Accel.Z) > .001f)
            {
                animCurveMinZ.AddKey(TimeStamp, vZ.X + Accel.Z);
                animCurveMaxZ.AddKey(TimeStamp, vZ.Y + Accel.Z);   
            }
            
            curScaleKey = curScaleKey.Scope.GetField("Next");
        }

        var curveX = new ParticleSystem.MinMaxCurve(1f, animCurveMinX, animCurveMaxX);
        var curveY = new ParticleSystem.MinMaxCurve(1f, animCurveMinY, animCurveMaxY);
        var curveZ = new ParticleSystem.MinMaxCurve(1f, animCurveMinZ, animCurveMaxZ);

        return new ParticleSystem.MinMaxCurve[3] {curveX, curveY, curveZ};
    }


    



    // From standard particle shader GUI source...
    private void SetMaterialBlendMode(UMaterial material, BlendMode blendMode)
    {           
        switch (blendMode)
        {
            case BlendMode.Opaque:
                material.SetOverrideTag("RenderType", "");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = -1;
                break;
            case BlendMode.Cutout:
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                break;
            case BlendMode.Fade:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
            case BlendMode.Transparent:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
            case BlendMode.Additive:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
            case BlendMode.Subtractive:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.ReverseSubtract);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
            case BlendMode.Modulate:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.EnableKeyword("_ALPHAMODULATE_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                break;
        }
    }



}
