using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using Voxar;
using Sd = System.Diagnostics;
public class novo : MonoBehaviour, Voxar.IReceiver<Voxar.BodyJoints[]>
{
    private bool begin;
    private bool andando;
    private BodyAngles Target;
    private bool turnFoot;
    Vector3 leftFoot;
    Vector3 rightFoot;
    GameObject ball;
    GameObject text; 
    public Text textRenderer;

    // Start is called before the first frame update
    void Start()
    {
        Target = new BodyAngles();
        Target.FrontalAngles.AddAngle(BoneType.LeftClavicule, BoneType.RightClavicule, 145.0f);
        turnFoot = false; // false == direita; true == esquerda
        leftFoot = new Vector3();
        rightFoot = new Vector3();
        ball = GameObject.Find("Sphere");
        text = GameObject.Find("Canvas/Text");
        text.SetActive(false);
        begin = true;
        andando = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ReceiveData(Voxar.BodyJoints[] bodys) {

        foreach(var body in bodys) {
            if (body.status == Status.Tracking)
            {
                Voxar.BodyAngles currentAngle = new Voxar.BodyAngles(body);
                var errors = MovementAnalyzer.CompareBodyAngles(currentAngle, Target, 10.0f);
                // MovementAnalyzer.CalculateAngleForBones(body, BoneType.RightClavicule, BoneType.LeftClavicule);

                // Debug.Log(currentAngle.FrontalAngles.GetAngle(BoneType.LeftClavicule, BoneType.RightClavicule));
                // pegar o angulo padrão entre eles!!!!
                bool entrei = false;
                foreach(var error in errors) { // Vejo se a postura tá boa.
                    entrei = true;
                    var angle = currentAngle.FrontalAngles.angles[error.boneTypes[0]][error.boneTypes[1]];
                    Debug.Log(angle);
                    text.SetActive(true);
                } 
                if (!entrei) text.SetActive(false);

                leftFoot = body.joints[JointType.LeftFoot].worldPosition/1000.0f;
                rightFoot = body.joints[JointType.RightFoot].worldPosition/1000.0f;
                var distanceFoots = Vector3.Distance(leftFoot, rightFoot);

                var vectors = MovementAnalyzer.GenerateLowerBodyPlanes(body);
                Vector3 myDirectionmyDirection = vectors[BasePlanes.Frontal];
                Vector3 mustBeTheNext;

                if (begin) {
                    mustBeTheNext = rightFoot;
                    ball.transform.localPosition = mustBeTheNext;
                    begin = false;
                }
                if(turnFoot == false && distanceFoots >= 0.5 && !andando) { // Pé direito
                    mustBeTheNext = rightFoot  /*myDirection.normalized * 0.0045f */;
                    ball.transform.localPosition = mustBeTheNext;
                    var cubeRenderer = ball.GetComponent<Renderer>();
                    cubeRenderer.material.SetColor("_Color", Color.red);                    
                    turnFoot = !turnFoot;
                    andando = true;
                }
                else if (distanceFoots >= 0.5 && turnFoot == true && !andando) { // Pé esquerdo
                    mustBeTheNext = leftFoot  /*myDirection.normalized * 0.0045f */;
                    ball.transform.localPosition = mustBeTheNext;
                    var cubeRenderer = ball.GetComponent<Renderer>();
                    cubeRenderer.material.SetColor("_Color", Color.blue);                                        
                    turnFoot = !turnFoot;
                    andando = true;
                }
                if(distanceFoots < 0.5 && andando) {
                    andando = false;
                }

                float cg = CenterOfGravity(body, 71.45f); // value just for test

                float p = body.joints[JointType.RightAnkle].worldPosition.z / 1000.0f;
                float q = body.joints[JointType.LeftAnkle].worldPosition.z / 1000.0f;
                float pt = (float)(Math.Truncate((double)p * 100.0) / 100.0);
                float qt = (float)(Math.Truncate((double)q * 100.0) / 100.0);

                float vp = (pt - qt)/(10*(Time.deltaTime));
                float vpp = 36*(Math.Abs(vp/10))/10;
                float vpt = (float)(Math.Truncate((double)vpp * 100.0) / 100.0);
                
                textRenderer.text = $"R {pt} Metros" + "\r\n" + $"L {qt} Metros" + "\r\n" + $"L {vpt} p/s";
                Debug.Log(p + "Meters");
            }
        }
    }
    float CenterOfGravity(BodyJoints body, float myMass) {
        Vector3 v = new Vector3(body.joints[JointType.Head].worldPosition.x,
        body.joints[JointType.Head].worldPosition.y, body.joints[JointType.Head].worldPosition.z);
        float mass = myMass; //kg
        float leftM, rightM;
        
        var vectors = MovementAnalyzer.GenerateLowerBodyPlanes(body);
        Vector3 sFrame = vectors[BasePlanes.Frontal];
        float A = sFrame.x,
        B = sFrame.y,
        C = sFrame.z;

        float angle = (float)Math.Asin(Math.Abs(A * v.x + B * v.y + C * v.z));

        if(angle == 90.0f){
            leftM = mass/ 2.0f;
            rightM = mass/ 2.0f;
        }

        float distanceFrom90 = 90.0f - angle;

        if (distanceFrom90 > 0){
            float leftMultiple = distanceFrom90 / 90.0f;
            leftM = mass * leftMultiple;
            rightM = mass - leftM;
        }
        else {
            float rightMultiple = distanceFrom90 / 90.0f;
            rightM = rightMultiple * mass;
            leftM = mass - rightMultiple;
        }


        // Calculado em relação a cabeça!
        var footLeft = body.joints[JointType.LeftFoot].worldPosition;
        var footRight = body.joints[JointType.RightFoot].worldPosition;

        float distanceFromDataumLeft = (float)Math.Sqrt(
            Math.Pow(v.x - footLeft.x, 2.0)
             + Math.Pow(v.y - footLeft.y, 2.0)
              + Math.Pow(v.z - footLeft.z, 2.0));
        
        float distanceFromDataumRight = (float)Math.Sqrt(
            Math.Pow(v.x - footRight.x, 2.0)
             + Math.Pow(v.y - footRight.y, 2.0)
              + Math.Pow(v.z - footRight.z, 2.0));

        
        float momentLeft = distanceFromDataumLeft *leftM;
        float momentRight = distanceFromDataumRight * rightM;

        float momentSum = momentLeft + momentRight;

        return momentSum / mass;
    }
}
