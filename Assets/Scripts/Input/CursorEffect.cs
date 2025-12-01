using UnityEngine;
using System.Collections;

public class CursorEffect : MonoBehaviour {


	// Update is called once per frame
	void Update () {

        if (UnifiedInput.UnifiedInputManager.GetMouseButtonDown(0))
        {
            ParticleSystem ps = this.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();
        }
	}
}
