using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public class TextWobble : MonoBehaviour
{
    [SerializeField] float xWobbleSpeed;
    [SerializeField] float yWobbleSpeed;

    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }


    private void Update()
    {

        text.ForceMeshUpdate();

        var textInfo = text.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {


            var charInfo = textInfo.characterInfo[i];
            var verts = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            if (!charInfo.isVisible || charInfo.character == ' ')
            {

                continue;



            }


            for (int j = 0; j < 4; ++j)
            {

                var orig = verts[charInfo.vertexIndex + j];

                verts[charInfo.vertexIndex + j] = orig + new Vector3(Mathf.Sin((Time.time + i) * xWobbleSpeed), Mathf.Cos((Time.time + i) * yWobbleSpeed));



            }

        }

        for (int i = 0; i < textInfo.meshInfo.Length; ++i)
        {



            var meshInfo = textInfo.meshInfo[i];

            meshInfo.mesh.vertices = meshInfo.vertices;

            text.UpdateGeometry(meshInfo.mesh, i);



        }







    }




}


