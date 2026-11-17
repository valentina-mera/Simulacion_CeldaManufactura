using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaboratorioRobotica
{
    /// <summary>
    /// Contiene todas las medidas reales tomadas en el laboratorio (según el documento "Medida Cinta_261110_094238.pdf")
    /// expresadas en metros para Unity (1 unidad de Unity = 1 metro, es decir 100 cm).
    /// También provee la cinemática paramétrica del circuito cerrado de la cinta transportadora Eshed Robotec.
    /// </summary>
    public static class ConveyorDimensions
    {
        // =========================================================================================
        // 1. MEDIDAS DE LA CINTA TRANSPORTADORA (ESHED ROBOTEC) - Página 2 del PDF
        // =========================================================================================
        /// <summary>Largo exterior total de la cinta: 484.5 cm (4.845 m)</summary>
        public const float TotalLength = 4.845f;

        /// <summary>Ancho exterior total del anillo de la cinta: 138.5 cm (1.385 m)</summary>
        public const float TotalWidth = 1.385f;

        /// <summary>Altura de la cinta respecto al piso: 96 cm (0.96 m)</summary>
        public const float BeltHeight = 0.960f;

        /// <summary>Ancho transversal del perfil/carril de la cinta: 17.3 cm (0.173 m)</summary>
        public const float TrackWidth = 0.173f;

        /// <summary>Ancho de cada hilera lateral de eslabones plásticos color crema: 4 cm (0.04 m)</summary>
        public const float SlatChainWidth = 0.040f;

        /// <summary>Paso (largo) de cada eslabón plástico en forma de medialuna: 3 cm (0.03 m)</summary>
        public const float SlatChainPitch = 0.030f;

        /// <summary>Espesor del bastidor/perfil de aluminio negro de la cinta: ~6.5 cm (0.065 m)</summary>
        public const float TrackFrameThickness = 0.065f;

        // Caja del motor Eshed Robotec (Página 2 y 3 del PDF)
        /// <summary>Largo de la caja negra del motor Eshed Robotec: 65 cm (0.65 m)</summary>
        public const float MotorBoxLength = 0.650f;

        /// <summary>Alto de la caja negra del motor Eshed Robotec: 42.4 cm (0.424 m)</summary>
        public const float MotorBoxHeight = 0.424f;

        /// <summary>Profundidad de la caja del motor: 18 cm (0.18 m)</summary>
        public const float MotorBoxDepth = 0.180f;

        // Caja lateral blanca de control/sensor (Página 2 del PDF)
        /// <summary>Largo de la caja lateral de control: 14.5 cm (0.145 m)</summary>
        public const float SideControlBoxLength = 0.145f;

        /// <summary>Alto de la caja lateral de control: 7.5 cm (0.075 m)</summary>
        public const float SideControlBoxHeight = 0.075f;

        // =========================================================================================
        // 2. MEDIDAS DE LA BANDEJA INFERIOR (PORTAPALLET BASE) - Página 1 del PDF
        // =========================================================================================
        /// <summary>Largo de la bandeja inferior: 20 cm (0.20 m)</summary>
        public const float LowerTrayLength = 0.200f;

        /// <summary>Ancho de la bandeja inferior: 16 cm (0.16 m)</summary>
        public const float LowerTrayWidth = 0.160f;

        /// <summary>Espesor/altura de la bandeja inferior: 1.9 cm (0.019 m)</summary>
        public const float LowerTrayHeight = 0.019f;

        /// <summary>Ancho de la ranura guía principal por debajo de la bandeja: 2.3 cm (0.023 m)</summary>
        public const float LowerTrayMainGrooveWidth = 0.023f;

        /// <summary>Ancho del riel/canal secundario por debajo de la bandeja: 1.7 cm (0.017 m)</summary>
        public const float LowerTraySecondaryGrooveWidth = 0.017f;

        // =========================================================================================
        // 3. MEDIDAS DE LA BANDEJA SUPERIOR (PORTAPIEZAS PERFORADA 010004) - Página 3 del PDF
        // =========================================================================================
        /// <summary>Largo de la bandeja superior perforada: 17.5 cm (0.175 m)</summary>
        public const float UpperTrayLength = 0.175f;

        /// <summary>Ancho de la bandeja superior perforada: 12.5 cm (0.125 m)</summary>
        public const float UpperTrayWidth = 0.125f;

        /// <summary>Altura del cuerpo de la bandeja superior perforada: 1.6 cm (0.016 m)</summary>
        public const float UpperTrayHeight = 0.016f;

        /// <summary>Altura de la manija central en T (para agarre del robot): 3.5 cm (0.035 m)</summary>
        public const float THandleHeight = 0.035f;

        /// <summary>Ancho superior del travesaño de la manija en T: 4.5 cm (0.045 m)</summary>
        public const float THandleTopWidth = 0.045f;

        /// <summary>Ancho del cuello/base de la manija en T: 2.3 cm (0.023 m)</summary>
        public const float THandleStemWidth = 0.023f;

        // =========================================================================================
        // 4. GEOMETRÍA PARAMÉTRICA DEL CIRCUITO CERRADO (ESTADIO / ÓVALO RECTANGULAR)
        // =========================================================================================
        // El eje central del carril (de ancho 17.3 cm) recorre un circuito de:
        // Ancho entre centros de carriles = TotalWidth - TrackWidth = 1.385 - 0.173 = 1.212 m
        // Radio de giro en los extremos curvos = 1.212 / 2 = 0.606 m
        // Largo de cada tramo recto = TotalLength - 2 * RadioExterior = 4.845 - 1.385 = 3.460 m

        public static float CenterlineSpanX => TotalWidth - TrackWidth; // 1.212 m
        public static float CurveRadius => CenterlineSpanX * 0.5f;      // 0.606 m
        public static float StraightLength => TotalLength - TotalWidth; // 3.460 m
        public static float HalfStraightZ => StraightLength * 0.5f;     // 1.730 m

        /// <summary>Longitud de arco de cada curva semicircular de 180 grados</summary>
        public static float CurveArcLength => Mathf.PI * CurveRadius;   // ~1.9038 m

        /// <summary>Perímetro total del eje central de la banda (~10.7276 metros)</summary>
        public static float TotalLoopPerimeter => (2f * StraightLength) + (2f * CurveArcLength);

        /// <summary>
        /// Evalúa la posición 3D (sobre la superficie superior de la cinta, Y = BeltHeight)
        /// y la dirección tangente de avance para cualquier distancia recorrida s (en metros).
        /// Sentido de giro:
        /// - Segmento 1 (Carril Derecho, X = +0.606 m): avanza de +Z hacia -Z (pasa por Estación 1: Inicio, Inventario)
        /// - Segmento 2 (Curva Izquierda/Sur, Z = -1.730 m): gira de X = +0.606 m hacia X = -0.606 m
        /// - Segmento 3 (Carril Izquierdo, X = -0.606 m): avanza de -Z hacia +Z (pasa por Estación 2: Manufactura)
        /// - Segmento 4 (Curva Derecha/Norte, Z = +1.730 m): gira de X = -0.606 m hacia X = +0.606 m (pasa por Estación 3: Almacenamiento)
        /// </summary>
        public static void EvaluateLoopPath(float distanceAlongLoop, out Vector3 position, out Vector3 tangent, out Vector3 outwardNormal)
        {
            float L = StraightLength;
            float C = CurveArcLength;
            float R = CurveRadius;
            float perimeter = TotalLoopPerimeter;

            float d = distanceAlongLoop % perimeter;
            if (d < 0f) d += perimeter;

            // Segmento 1: Tramo recto Derecho (X = +R, Z va de +L/2 hacia -L/2)
            if (d < L)
            {
                float z = HalfStraightZ - d;
                position = new Vector3(R, BeltHeight, z);
                tangent = Vector3.back;
                outwardNormal = Vector3.right;
                return;
            }
            d -= L;

            // Segmento 2: Curva Sur / Izquierda (Centro en (0, BeltHeight, -L/2), gira de +X a -X)
            if (d < C)
            {
                float angle = -(d / C) * Mathf.PI; // 0 en +X, -PI/2 en -Z, -PI en -X
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);
                position = new Vector3(R * cosA, BeltHeight, -HalfStraightZ + R * sinA);
                tangent = new Vector3(sinA, 0f, -cosA).normalized;
                outwardNormal = new Vector3(cosA, 0f, sinA).normalized;
                return;
            }
            d -= C;

            // Segmento 3: Tramo recto Izquierdo (X = -R, Z va de -L/2 hacia +L/2)
            if (d < L)
            {
                float z = -HalfStraightZ + d;
                position = new Vector3(-R, BeltHeight, z);
                tangent = Vector3.forward;
                outwardNormal = Vector3.left;
                return;
            }
            d -= L;

            // Segmento 4: Curva Norte / Derecha (Centro en (0, BeltHeight, +L/2), gira de -X a +X)
            {
                float angle = Mathf.PI - (d / C) * Mathf.PI; // PI en -X, PI/2 en +Z, 0 en +X
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);
                position = new Vector3(R * cosA, BeltHeight, HalfStraightZ + R * sinA);
                tangent = new Vector3(sinA, 0f, -cosA).normalized;
                outwardNormal = new Vector3(cosA, 0f, sinA).normalized;
            }
        }

        // =========================================================================================
        // 5. UTILIDADES DE MATERIALES MULTI-RENDER PIPELINE (BUILT-IN / URP / HDRP)
        // =========================================================================================
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        /// <summary>
        /// Crea o reutiliza un Material compatible automáticamente con Built-in Standard, URP o HDRP,
        /// garantizando que nunca se vean objetos rosados/magenta sin importar la plantilla de Unity usada.
        /// </summary>
        public static Material GetOrCreateMaterial(string key, Color color, float metallic = 0.2f, float smoothness = 0.5f, bool emissive = false, Color emissionColor = default)
        {
            string fullKey = $"{key}_{color}_{metallic:F2}_{smoothness:F2}_{emissive}_{emissionColor}";
            if (MaterialCache.TryGetValue(fullKey, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader)
            {
                name = key,
                color = color
            };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                Color finalEmission = emissionColor == default ? color * 1.8f : emissionColor;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", finalEmission);
            }

            MaterialCache[fullKey] = mat;
            return mat;
        }
    }
}

