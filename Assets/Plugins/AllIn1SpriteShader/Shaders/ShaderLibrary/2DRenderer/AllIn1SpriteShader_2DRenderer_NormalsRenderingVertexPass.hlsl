#ifndef ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGVERTEXPASS
#define ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGVERTEXPASS

FragmentDataNormalsPass NormalsRenderingVertex(VertexDataNormalsPass v)
{
	#if RECTSIZE_ON
	v.vertex.xyz += (v.vertex.xyz * (_RectSize - 1.0));
	#endif

    FragmentDataNormalsPass o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	#if BILBOARD_ON
	half3 camRight = mul((half3x3)unity_CameraToWorld, half3(1,0,0));
	half3 camUp = half3(0,1,0);
	#if BILBOARDY_ON
	camUp = mul((half3x3)unity_CameraToWorld, half3(0,1,0));
	#endif
	half3 localPos = v.vertex.x * camRight + v.vertex.y * camUp;
	o.vertex = TransformObjectToHClip(localPos);
	#else
	o.vertex = TransformObjectToHClip(v.vertex.xyz);
	#endif
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	o.color = v.color;

	half2 center = half2(0.5, 0.5);
	#if ATLAS_ON
	center = half2((_MaxXUV + _MinXUV) / 2.0, (_MaxYUV + _MinYUV) / 2.0);
	#endif

	#if POLARUV_ON
	o.uv = v.uv - center;
	#endif

	#if ROTATEUV_ON
	half2 uvC = v.uv;
	half cosAngle = cos(_RotateUvAmount);
	half sinAngle = sin(_RotateUvAmount);
	half2x2 rot = half2x2(cosAngle, -sinAngle, sinAngle, cosAngle);
	uvC -= center;
	o.uv = mul(rot, uvC);
	o.uv += center;
	#endif

	#if OUTTEX_ON
	o.uvOutTex = TRANSFORM_TEX(v.uv, _OutlineTex);
	#endif

	#if OUTDIST_ON
	o.uvOutDistTex = TRANSFORM_TEX(v.uv, _OutlineDistortTex);
	#endif

	#if DISTORT_ON
	o.uvDistTex = TRANSFORM_TEX(v.uv, _DistortTex);
	#endif

    o.normalWS = -GetViewForwardDir();
    o.tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
    o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w;

    return o;
}

#endif //ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGVERTEXPASS