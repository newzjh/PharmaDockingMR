using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{

    public class Orbit : MonoBehaviour
    {
        public Atom sourceAtom;
        public Atom targetAtom;

        // Use this for initialization

        private float rotateTime = 0.0f;
        private Transform currentParent = null;
        void Start() {
	
            Vector3 angle;
            angle.x = UnityEngine.Random.Range(0.0f, 360.0f);
            angle.y = UnityEngine.Random.Range(-89.0f, 89.0f);
            angle.z = UnityEngine.Random.Range(0.0f, 360.0f);
            this.transform.localEulerAngles = angle;
            rotateTime = 0.0f;
            currentParent = null;
	    }

        // Update is called once per frame
        void Update()
        {
            Vector3 angle=Vector3.zero;
            angle.y=Time.deltaTime*120.0f;
            this.transform.Rotate(angle, Space.Self);

            if (targetAtom != null && targetAtom.transform && sourceAtom != null && sourceAtom.transform)
            {
                if (currentParent == null)
                {
                    currentParent = sourceAtom.transform;
                    Vector3 cUp = this.transform.up;
                    this.transform.position = currentParent.position;
                    this.transform.right = sourceAtom.transform.position - targetAtom.transform.position;
                    rotateTime = UnityEngine.Random.Range(0.0f, 3.0f);
                    Vector3 anglea = Vector3.zero;
                    anglea.y = rotateTime * 120.0f;
                    this.transform.Rotate(anglea, Space.Self);
                    return;
                }
                if (rotateTime >= 3.0f)
                {
                    if (currentParent == sourceAtom.transform)
                    {
                        currentParent = targetAtom.transform;
                        Vector3 cUp = this.transform.up;
                        this.transform.position = currentParent.position;
                        this.transform.up = cUp * -1.0f;
                    }
                    else if (currentParent == targetAtom.transform)
                    {
                        currentParent = sourceAtom.transform;
                        Vector3 cUp = this.transform.up;
                        this.transform.position = currentParent.position;
                        this.transform.up = cUp * -1.0f;
                        this.transform.right = sourceAtom.transform.position - targetAtom.transform.position;
                    }
                    rotateTime = 0.0f;
                }
                rotateTime += Time.deltaTime;
            }
        }
    }

}
