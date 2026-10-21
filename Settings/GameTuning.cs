using System;
using UnityEngine;

// One place for every "feel" number of the game: player speeds, air, how far you see in murky water,
// fog, torch, the sea. Lives as an asset (Settings/Resources/GameTuning.asset), so it is the same in
// every scene and survives leaving Play Mode: tweak it while playing and the change stays. Open it with
// Horror > Ajustes do jogo, or click the [GameTuning] object that appears in the hierarchy in Play Mode.
// Consumers read GameTuning.Instance and listen to Changed, so edits show up immediately.
[CreateAssetMenu(fileName = "GameTuning", menuName = "Horror/Game Tuning")]
public sealed class GameTuning : ScriptableObject
{
    public const string ResourceName = "GameTuning";

    [Serializable]
    public struct RoomVisibility
    {
        [Tooltip("Nome exato do objeto WaterVolume na cena: BasementWater, BedroomWater, ShaftAndCorridorWater, CrossPassageWater, TankRoomWater ou AirPocketWater.")]
        public string volume;

        [Tooltip("Metros até não se ver mais nada nesse cômodo.")]
        public float metres;
    }

    [Header("Jogador")]
    public float walkSpeed = 2.9f;
    public float runSpeed = 4.8f;
    public float crouchSpeed = 1.4f;
    public float wadeSpeed = 1.8f;
    public float swimSpeed = 2.1f;
    public float sprintSwimSpeed = 3.4f;
    public float verticalSwimSpeed = 1.8f;

    [Tooltip("Segundos de fôlego embaixo d'água.")]
    public float airSeconds = 60f;

    [Tooltip("Com o ar zerado embaixo d'água: segundos até o personagem perder a consciência e morrer. É a janela para buscar ar.")]
    public float drownSeconds = 12f;

    [Tooltip("Respirando de novo: segundos para a vida voltar de zero ao máximo.")]
    public float recoverSeconds = 10f;

    [Tooltip("Depois de um golpe de criatura: segundos para a vida voltar de zero ao máximo (começa a fechar 4 s depois do último golpe).")]
    [Range(5f, 120f)] public float blowRecoverSeconds = 40f;

    [Header("Água: visão (turbidez)")]
    public Color underwaterFogColor = new Color(0.043f, 0.165f, 0.18f);

    [Tooltip("Metros de visão embaixo d'água: depois dessa distância não se vê nada. Vale para toda a água.")]
    public float visibility = 7f;

    [Tooltip("Opcional: um cômodo que deva ser mais turvo ou mais limpo que o resto. Vazio = todos iguais.")]
    public RoomVisibility[] exceptions = new RoomVisibility[0];

    [Tooltip("Embaixo d'água, tudo mais perto que isso (metros) fica nítido.")]
    public float sharpDistance = 3.2f;

    [Tooltip("Distância (metros) em que o desfoque embaixo d'água chega ao máximo.")]
    public float blurEndDistance = 10f;

    [Header("Névoa volumétrica")]
    [Range(0f, 2f)] public float houseFogDensity = 0.8f;
    [Range(0f, 2f)] public float houseFogScattering = 0.8f;
    public float houseFogAttenuation = 18f;
    [Range(0f, 2f)] public float underwaterFogDensity = 0.12f;
    [Range(0f, 2f)] public float underwaterFogScattering = 0.12f;
    public float underwaterFogAttenuation = 6f;

    [Header("Lanterna de mergulho")]
    public float torchIntensity = 70f;
    public float torchRange = 22f;
    // One angle only: the torch's procedural cookie draws the hot spot, reflector ring and spill across
    // the whole cone, so URP's inner angle barely showed. The inner angle now follows this one (22/60).
    [Tooltip("Abertura do facho da lanterna, em graus.")]
    [Range(10f, 120f)] public float torchAngle = 60f;

    public float TorchInnerAngle => torchAngle * (22f / 60f);

    [Tooltip("Brilho do facho visível saindo da lente, no ar (poeira).")]
    [Range(0f, 3f)] public float torchBeamAir = 0.12f;

    [Tooltip("Brilho do facho visível saindo da lente, embaixo d'água (sujeira em suspensão).")]
    [Range(0f, 3f)] public float torchBeamWater = 0.45f;

    [Tooltip("Metros de facho desenhado a partir da lente; depois disso, só a névoa volumétrica.")]
    [Range(0.5f, 6f)] public float torchBeamLength = 3.5f;

    [Tooltip("O facho (só a luz, a mão fica como a animação) vira para o que está sob a mira, perto ou longe, mas nunca "
             + "para um ponto mais perto que isto (metros).")]
    [Range(0.2f, 3f)] public float torchAimMinDistance = 0.6f;

    [Tooltip("Quantos graus, no máximo, o facho pode virar em relação à lanterna na mão para cair sob a mira (0 = o "
             + "facho sai reto da lanterna animada).")]
    [Range(0f, 45f)] public float torchAimTurn = 20f;

    [Tooltip("Segundos até o facho erguido (botão direito) chegar ao máximo de força e fechamento.")]
    [Range(0.05f, 1f)] public float torchFocusSeconds = 0.18f;

    [Tooltip("Erguida: o facho fecha para esta fração do ângulo normal e fica mais forte por este fator.")]
    [Range(0.2f, 1f)] public float torchFocusAngle = 0.5f;

    [Range(1f, 4f)] public float torchFocusBoost = 1.8f;

    [Tooltip("Atordoar com a luz: cone (graus a partir do centro do facho) em que os olhos da criatura têm de estar.")]
    [Range(2f, 30f)] public float torchStunAngle = 9f;

    [Tooltip("Atordoar: distância máxima (metros) dos olhos à lanterna.")]
    [Range(1f, 20f)] public float torchStunRange = 9f;

    [Tooltip("Atordoar: segundos seguidos de luz erguida nos olhos para a criatura ser atordoada.")]
    [Range(0.05f, 3f)] public float torchStunExposure = 0.5f;

    [Tooltip("Atordoar: quanto tempo ela fica atordoada (segundos).")]
    [Range(0.5f, 10f)] public float torchStunSeconds = 3f;

    [Tooltip("Atordoar: depois de passar o atordoamento, quantos segundos ela fica imune à luz.")]
    [Range(0f, 20f)] public float torchStunImmunity = 5f;

    [Tooltip("Segundos para sair da água por uma borda (a animação de escalada é esticada para durar isso; a original tem 4 s).")]
    [Range(1.5f, 6f)] public float climbOutSeconds = 3.2f;

    [Tooltip("Aceleração ao começar a andar (m/s por segundo). Menor = arrancada mais pesada.")]
    [Range(2f, 40f)] public float walkAcceleration = 9f;

    [Tooltip("Frenagem ao soltar as teclas (m/s por segundo). Menor = para mais devagar.")]
    [Range(2f, 40f)] public float walkDeceleration = 12f;

    [Header("Corpo do jogador")]
    [Tooltip("Rapidez com que a câmera acompanha os olhos da cabeça animada em terra. Menor = menos balanço.")]
    [Range(2f, 60f)] public float headCameraFollow = 18f;

    [Tooltip("Na água: quanto a cabeça do corpo fica atrás da câmera (metros). Em terra a câmera fica nos olhos.")]
    [Range(-0.1f, 0.5f)] public float bodyBehindEye = 0.15f;

    [Tooltip("Nadando, quanto a cabeça do corpo fica abaixo da câmera (metros).")]
    [Range(0f, 0.4f)] public float swimHeadBelowEye = 0.05f;

    [Tooltip("Quanto a coluna acompanha a câmera ao olhar para cima e para baixo (0 a 1).")]
    [Range(0f, 1f)] public float bodyLookWeight = 0.15f;

    [Tooltip("Com roupa volumosa (traje com máscara), o máximo que a cabeça abaixa ao olhar para baixo (graus). A câmera "
             + "continua girando; só o olho desce menos, senão entra no peito do traje.")]
    [Range(0f, 90f)] public float bulkyHeadPitch = 35f;

    [Tooltip("Sem roupa volumosa, o máximo que a cabeça abaixa (graus). A câmera continua até o limite dela; acima disso "
             + "a cabeça torcia de lado e o olho entrava no peito (vídeo do Gustavo, 03/10).")]
    [Range(0f, 90f)] public float headPitch = 60f;

    [Tooltip("Parado, quantos graus a vista pode virar antes de o corpo girar junto. Olhando para baixo isso cai até "
             + "lookDownTurnAt, para o tronco ficar debaixo da vista.")]
    [Range(10f, 120f)] public float turnInPlaceAt = 75f;

    [Range(5f, 60f)] public float lookDownTurnAt = 15f;

    [Header("Braços em primeira pessoa: o que é procedural (as poses são os clipes de Art/Animations/FirstPerson)")]
    [Tooltip("Graus de dispersão a mais andando e correndo (a mira abre junto).")]
    [Range(0f, 6f)] public float revolverMoveSpread = 2f;

    [Tooltip("Graus de dispersão a mais por tiro seguido, que somem em revolverBloomSeconds.")]
    [Range(0f, 5f)] public float revolverShotBloom = 1.6f;

    [Range(0.1f, 2f)] public float revolverBloomSeconds = 0.55f;

    [Tooltip("Quanto a arma atrasa e volta com mola ao virar a vista (0 = presa à câmera).")]
    [Range(0f, 3f)] public float revolverSway = 1f;

    [Tooltip("Balanço da arma com os passos: força geral (0 = desliga).")]
    [Range(0f, 3f)] public float revolverBob = 1f;

    [Tooltip("Ligado: o balanço segue os passos da animação do corpo (um 8 por ciclo de dois passos). Desligado: segue a "
             + "distância andada (revolverBobStride).")]
    public bool revolverBobSyncSteps = true;

    [Tooltip("Ritmo do balanço sobre o ritmo escolhido acima (1 = igual aos passos; 0,5 = metade da velocidade).")]
    [Range(0.1f, 2f)] public float revolverBobRate = 1f;

    [Tooltip("Sem sincronizar com os passos: metros andados por ciclo de balanço (maior = mais lento).")]
    [Range(0.5f, 6f)] public float revolverBobStride = 2.9f;

    [Tooltip("Desloca o balanço dentro do passo (0 a 1), para a descida coincidir com o pé tocando o chão.")]
    [Range(0f, 1f)] public float revolverBobPhase = 0f;

    [Tooltip("Tamanho do balanço andando: lado e altura (metros).")]
    public Vector2 revolverBobMove = new Vector2(0.011f, 0.007f);

    [Tooltip("Tamanho do balanço andando: giro (graus): inclinar para cima e baixo, rolar de lado.")]
    public Vector2 revolverBobTurn = new Vector2(0.9f, 1.6f);

    [Tooltip("Correndo com a arma (clipe Revolver_Sprint): tamanho do balanço dos braços com os passos: lado e altura (metros). "
             + "É o que faz a mão bombear com a arma como nos jogos de tiro.")]
    public Vector2 revolverSprintBobMove = new Vector2(0.022f, 0.032f);

    [Tooltip("Correndo com a arma: giro do balanço (graus): inclinar para cima e baixo, rolar de lado.")]
    public Vector2 revolverSprintBobTurn = new Vector2(3f, 6f);

    [Tooltip("Correndo com a LANTERNA (ainda sem clipe próprio): quanto ela abaixa e cruza, a partir da pose (metros: direita, cima, frente).")]
    public Vector3 torchSprintOffset = new Vector3(-0.03f, 0.04f, -0.02f);

    [Tooltip("Correndo com a lanterna: giro (graus): para baixo, para o lado, rolar.")]
    public Vector3 torchSprintEuler = new Vector3(6f, -6f, 10f);

    [Tooltip("Respiração da arma parado.")]
    [Range(0f, 3f)] public float revolverBreath = 1f;

    [Tooltip("Segundos da arma subindo de baixo ao sacar.")]
    [Range(0.05f, 1f)] public float revolverDrawSeconds = 0.35f;

    [Tooltip("Coice: quantos graus o cano sobe num tiro (a arma gira pelo cabo).")]
    [Range(0f, 40f)] public float revolverRecoilFlip = 16f;

    [Tooltip("Coice: quanto a arma vem para trás e para cima (metros).")]
    [Range(0f, 0.2f)] public float revolverRecoilBack = 0.07f;

    [Range(0f, 0.08f)] public float revolverRecoilUp = 0.025f;

    [Tooltip("Coice: giro de lado aleatório (graus).")]
    [Range(0f, 20f)] public float revolverRecoilRoll = 7f;

    [Tooltip("Coice: rapidez da volta (maior = volta mais rápido) e quanto ele passa do ponto na volta (0 = sem, 1 = muito).")]
    [Range(100f, 800f)] public float revolverRecoilSpring = 320f;

    [Range(0f, 1f)] public float revolverRecoilBounce = 0.35f;

    [Tooltip("Mirando pela alça, o coice da arma é multiplicado por isto.")]
    [Range(0f, 1f)] public float revolverRecoilAimed = 0.55f;

    [Tooltip("Coice na câmera: graus que a vista sobe num tiro e a fração que volta sozinha.")]
    [Range(0f, 8f)] public float revolverCameraKick = 2.8f;

    [Range(0f, 1f)] public float revolverCameraKickReturn = 0.65f;

    [Tooltip("Clarão do tiro: brilho máximo da luz na boca do cano e quanto tempo ela leva para apagar (segundos).")]
    [Range(0f, 80f)] public float revolverFlashIntensity = 35f;

    [Range(0.02f, 0.3f)] public float revolverFlashSeconds = 0.07f;

    [Header("Esporos e criaturas")]
    [Tooltip("Segundos que um filtro da máscara aguenta nos esporos densos (nos leves dura mais).")]
    [Range(10f, 300f)] public float filterSeconds = 70f;

    [Tooltip("Sem proteção, segundos de esporos densos até o Erick desmaiar.")]
    [Range(3f, 60f)] public float sporeHarmSeconds = 14f;

    [Tooltip("Esporos leves (perto dos fungos) só fazem tossir: a vida não cai abaixo disso.")]
    [Range(0f, 1f)] public float sporeHarmFloor = 0.4f;

    [Tooltip("Respiração abafada da máscara nos esporos.")]
    [Range(0f, 1f)] public float maskBreathVolume = 0.35f;

    [Tooltip("Afogado: quanto da vida um golpe em pé tira (0 a 1).")]
    [Range(0f, 1f)] public float drownedBlowDamage = 0.34f;

    [Tooltip("Afogado: quanto da vida uma mordida rastejando tira (0 a 1).")]
    [Range(0f, 1f)] public float drownedBiteDamage = 0.2f;

    [Tooltip("Afogado: distância em que ele percebe o jogador com linha de visão livre (metros).")]
    [Range(3f, 30f)] public float drownedNoticeRange = 11f;

    [Tooltip("Afogado: distância em que ele percebe o jogador mesmo sem ver (passos, atrás de uma porta).")]
    [Range(0f, 8f)] public float drownedHearRange = 2.5f;

    [Header("Final da demo (o esconderijo: GDD \"O fim da demo\", 09/10)")]
    [Tooltip("Segundos por bala carregada devagar.")]
    [Range(1f, 10f)] public float finaleSecondsPerRound = 4f;

    [Tooltip("Em que bala o relâmpago entrega o Erick (caminho lento).")]
    [Range(1, 6)] public int finaleLightningRound = 4;

    [Tooltip("Câmera lenta ao se erguer para atirar (1 = tempo normal).")]
    [Range(0.2f, 1f)] public float finaleSlowMotion = 0.5f;

    [Tooltip("Segundos (de relógio) da câmera lenta.")]
    [Range(0.5f, 4f)] public float finaleSlowSeconds = 1.5f;

    [Tooltip("Distância do Afogado em que o Erick atira sozinho se o jogador não atirar (metros).")]
    [Range(0.8f, 5f)] public float finaleAutoShotDistance = 2f;

    [Tooltip("Segundos de preto depois do tiro até o título.")]
    [Range(1f, 8f)] public float finaleTitleDelay = 3f;

    [Tooltip("Velocidade do Afogado procurando (1 = andar do clipe).")]
    [Range(0.3f, 1.2f)] public float finaleSearchSpeed = 0.65f;

    [Header("Tentáculos (polvo colossal e tentáculo solto)")]
    [Tooltip("Graus de curvatura por osso na ponta do braço, na onda que corre da base à ponta.")]
    [Range(0f, 20f)] public float tentacleWave = 7f;

    [Tooltip("Velocidade da onda (radianos por segundo).")]
    [Range(0f, 6f)] public float tentacleWaveSpeed = 1.2f;

    [Tooltip("Enrolar constante da ponta, em graus por osso (polvo em repouso enrola as pontas).")]
    [Range(-15f, 15f)] public float tentacleCurl = 3f;

    [Tooltip("Graus que um tiro joga o braço atingido para trás, por unidade de impulso da bala.")]
    [Range(0f, 30f)] public float tentacleRecoil = 7f;

    [Tooltip("Reflexo de dor: graus por osso que o braço atingido enrola para dentro depois do tiro (os outros, um terço).")]
    [Range(0f, 15f)] public float tentacleFlinch = 5f;

    [Header("Máscara vista de dentro (visor)")]
    [Tooltip("Sujeira e riscos do vidro, que aparecem onde há luz.")]
    [Range(0f, 1f)] public float maskDirt = 0.6f;

    [Tooltip("Embaçado que fica no vidro o tempo todo (nas bordas).")]
    [Range(0f, 1f)] public float maskFogBase = 0.12f;

    [Tooltip("Embaçado de cada expiração, que some ao inspirar.")]
    [Range(0f, 1f)] public float maskFogBreath = 0.32f;

    [Tooltip("Segundos de uma respiração com a máscara, parado ou andando.")]
    [Range(1.5f, 8f)] public float maskBreathSeconds = 3.8f;

    [Tooltip("Segundos de uma respiração correndo (ofegante, embaça mais).")]
    [Range(0.8f, 4f)] public float maskBreathSecondsRunning = 1.7f;

    [Tooltip("Segundos para a máscara aparecer ou sumir da tela ao pôr ou tirar.")]
    [Range(0.05f, 2f)] public float maskFadeSeconds = 0.5f;

    [Tooltip("Trinca que a máscara velha do sótão já tem ao ser vestida (0 = inteira, 1 = toda trincada).")]
    [Range(0f, 1f)] public float maskCrackStart = 0.18f;

    [Tooltip("Segundos para o visor secar depois de sair da água ou da chuva.")]
    [Range(5f, 120f)] public float maskDrySeconds = 35f;

    [Tooltip("Segundos de chuva descoberta até o visor ficar cheio de gotas.")]
    [Range(1f, 30f)] public float maskRainWetSeconds = 6f;

    [Tooltip("Brilho das luzes espalhado pela sujeira do vidro.")]
    [Range(0f, 2f)] public float maskGlare = 1f;

    [Tooltip("Quanto o reflexo do vidro desliza ao virar a cabeça.")]
    [Range(0f, 2f)] public float maskSheenFollow = 1f;

    [Tooltip("Velocidade da criatura na perseguição (m/s). Andando: 2,9; correndo: 4,8.")]
    [Range(1f, 6f)] public float chaseSpeed = 3.9f;

    [Header("Luminárias da casa (todas de uma vez)")]
    [Tooltip("Multiplica o brilho da luz principal de todas as luminárias elétricas (1 = como foram montadas).")]
    [Range(0f, 6f)] public float lampBrightness = 3.5f;

    [Tooltip("Multiplica o alcance das luminárias.")]
    [Range(0.2f, 3f)] public float lampRange = 1.8f;

    [Tooltip("Multiplica a luz de rebatimento falsa (pontual sem sombra que enche a sala). 0 desde 04/10: ela atravessava paredes e lavava a sala de cinza; com a exposição automática a sala lê melhor só com a luz real.")]
    [Range(0f, 3f)] public float lampBounce = 1.2f;

    [Tooltip("Halo das luminárias na névoa volumétrica. 0 = sem halo (padrão desde 29/09: com 1 o bloom estourava).")]
    [Range(0f, 2f)] public float lampFogGlow = 0f;

    [Header("Vitral e vidro fosco do hall (arte da casa, 13/10)")]
    [Tooltip("Brilho da noite atravessando o vitral (o vidro visto de dentro). 0 = vitral escuro até o relâmpago.")]
    [Range(0f, 1f)] public float stainedGlassGlow = 0.3f;

    [Tooltip("Quanto o relâmpago acende o vitral (multiplica o clarão).")]
    [Range(0f, 20f)] public float stainedGlassFlash = 5f;

    [Tooltip("Luz colorida que o vitral joga no hall, parada (a noite lá fora). A luz vem de 6 m, de fora, e cai no piso escuro: valores altos (6 mal se vê, como uma noite nublada).")]
    [Range(0f, 30f)] public float stainedGlassLight = 6f;

    [Tooltip("Luz colorida do vitral no instante do relâmpago (somada à parada, vezes o clarão).")]
    [Range(0f, 300f)] public float stainedGlassLightFlash = 120f;

    [Tooltip("Quanto a luz colorida do vitral aparece na névoa (feixe colorido no ar).")]
    [Range(0f, 2f)] public float stainedGlassFog = 0.6f;

    [Tooltip("Desfoque do vidro fosco da porta do pátio por metro de distância atrás do vidro (fração da tela). Encostado no vidro fica nítido; longe vira mancha.")]
    [Range(0f, 0.2f)] public float frostedBlurPerMetre = 0.05f;

    [Header("Iluminação: sombras e itens")]
    [Tooltip("Mapas de sombra para as luzes perto do jogador (luz pontual custa 6, spot custa 1). Sem sombra, a luz atravessa paredes; mais mapas = mais custo de GPU.")]
    [Range(0, 192)] public int shadowMaps = 96;

    [Tooltip("Só disputa sombra a luz cujo alcance chega até esta distância (metros) da câmera.")]
    public float shadowReach = 4f;

    [Tooltip("As luzes de rebatimento das luminárias (pontuais, nome terminando em Bounce) também disputam sombra. Desligado, só as luzes principais ganham mapa (cada rebatimento custa 6).")]
    public bool fillShadows = true;

    [Tooltip("Rebatimento só onde o jogador vê: a luz de rebatimento de um cômodo que não está à vista apaga (FillCulling). Medido na build 13/10: era o que mais pesava (biblioteca 19 para ~11 ms de GPU) e iluminava os cômodos vizinhos através das paredes.")]
    public bool fillCulling = true;

    [Tooltip("Segundos para um rebatimento acender ou apagar quando entra ou sai da vista.")]
    [Range(0.05f, 3f)] public float fillFadeSeconds = 0.6f;

    [Tooltip("Brilho do contorno dos itens que dá para pegar (0 = sem brilho).")]
    [Range(0f, 3f)] public float itemGlow = 1f;

    [Tooltip("Até quantos metros o contorno dos itens aparece (some aos poucos até lá).")]
    public float itemGlowDistance = 6f;

    [Header("Imersão (tela e som do corpo: mirar, dano, correr, medo)")]
    [Tooltip("Força de todas as bordas escuras (vinheta) da imersão juntas: mirar, correr, medo e golpe. Vale ao entrar em Play.")]
    [Range(0f, 1.5f)] public float immersionVignette = 0.6f;

    [Tooltip("Mirando (revólver ou lanterna erguida): quanto a borda escurece.")]
    [Range(0f, 0.6f)] public float aimVignette = 0.2f;

    [Tooltip("Mirando: quantos graus o campo de visão fecha (a vista se concentra).")]
    [Range(0f, 25f)] public float aimZoom = 14f;

    [Tooltip("Mirando: quanto o som do mundo abafa (0 a 1), o corpo prendendo a respiração.")]
    [Range(0f, 0.6f)] public float aimMuffle = 0.15f;

    [Tooltip("Volume da respiração ao começar e ao parar de mirar.")]
    [Range(0f, 1f)] public float aimBreathVolume = 0.35f;

    [Tooltip("Força geral dos efeitos de um golpe (clarão vermelho, aberração, distorção, zumbido, sangue na tela).")]
    [Range(0f, 2f)] public float hurtStrength = 1f;

    [Tooltip("Segundos que o efeito de um golpe leva para passar.")]
    [Range(0.3f, 5f)] public float hurtSeconds = 1.6f;

    [Tooltip("Sangue na tela num golpe forte (0 = nunca).")]
    [Range(0f, 1f)] public float hurtBloodScreen = 0.8f;

    [Tooltip("Correndo: segundos de corrida até o fôlego acabar (respiração ofegante no máximo).")]
    [Range(1f, 20f)] public float exertionSeconds = 7f;

    [Tooltip("Segundos para recuperar o fôlego depois de parar.")]
    [Range(1f, 30f)] public float exertionRecoverSeconds = 9f;

    [Tooltip("Volume da respiração ofegante no máximo do cansaço.")]
    [Range(0f, 1f)] public float exertionBreathVolume = 0.5f;

    [Tooltip("Correndo: quantos graus o campo de visão abre e quanto borrão de movimento entra.")]
    [Range(0f, 12f)] public float sprintFov = 5f;

    [Range(0f, 1f)] public float sprintMotionBlur = 0.25f;

    [Tooltip("Medo: distância (metros) a partir da qual uma criatura começa a assustar; no máximo a ~2 m.")]
    [Range(3f, 30f)] public float fearRange = 14f;

    [Tooltip("Medo: força geral (batimento, drone, cor, granulação, borda).")]
    [Range(0f, 2f)] public float fearStrength = 1f;

    [Tooltip("Medo: granulação do filme no máximo do medo (0 = nenhuma). Vale ao entrar em Play.")]
    [Range(0f, 1f)] public float fearGrain = 0.2f;

    [Range(0f, 1f)] public float heartbeatVolume = 0.55f;

    [Range(0f, 1f)] public float dreadDroneVolume = 0.35f;

    [Header("Exposição automática (o olho se adaptando)")]
    [Tooltip("Liga a adaptação do olho: olhar para uma luz fecha a exposição (sem bloom estourado), voltar para o escuro abre devagar.")]
    public bool autoExposure = true;

    [Tooltip("Luminância média da cena que fica como está. Cena mais clara que isso: o olho fecha; mais escura: abre.")]
    public float exposureKey = 0.045f;

    [Tooltip("Quanto o olho pode abrir no escuro (em stops). Pouco, senão o escuro deixa de ser escuro.")]
    [Range(0f, 3f)] public float exposureMaxUp = 2f;

    [Tooltip("Quanto o olho pode fechar olhando para luz forte (em stops).")]
    [Range(0f, 5f)] public float exposureMaxDown = 2.5f;

    [Tooltip("Segundos para fechar quando a luz aumenta.")]
    public float exposureCloseSeconds = 0.9f;

    [Tooltip("Segundos para abrir quando a luz diminui (o olho se acostuma ao escuro devagar).")]
    public float exposureOpenSeconds = 4f;

    [Header("Luz rebatida (SSGI)")]
    [Tooltip("Luz rebatida calculada na tela (SSGI, pacote grátis). Desligada por padrão: medido em 04/10, nesta casa ela troca a luz ambiente por uma luz rebatida mais fraca e a imagem escurece; custa uma passada extra de geometria.")]
    public bool ssgi = false;

    [Tooltip("Força da luz rebatida.")]
    [Range(0f, 3f)] public float ssgiIntensity = 1f;

    [Header("Luz própria no escuro")]
    public float darkVisionIntensity = 1f;
    public float darkVisionUnderwater = 0.8f;

    [Header("Mar")]
    [Tooltip("Centro da ilha no plano do mar (x, z): as ondas andam em direção a ele.")]
    public Vector2 islandCenter = Vector2.zero;

    [Tooltip("Dentro deste raio (metros) a água fica abrigada: sem ondulação.")]
    public float shoreRadius = 12f;

    [Tooltip("Deste raio (metros) para fora a ondulação tem a altura cheia.")]
    public float openSeaRadius = 70f;

    [Tooltip("Altura da onda em mar aberto, do vale à crista (metros).")]
    public float swellHeight = 1.2f;

    public float swellLength = 22f;

    [Tooltip("Marola de vento por cima da ondulação (metros); também acalma perto da costa, mas menos.")]
    public float chopHeight = 0.25f;

    public float waveSpeed = 1f;

    [Header("Som")]
    [Tooltip("Chuva lá fora, ouvida ao ar livre. Coberto (debaixo de teto), cai para o volume de 'coberto' abaixo.")]
    [Range(0f, 1f)] public float rainVolume = 0.22f;

    [Range(0f, 1f)] public float thunderVolume = 0.8f;

    [Range(0f, 1f)] public float seaVolume = 0.3f;

    [Range(0f, 1f)] public float windVolume = 0.3f;

    [Tooltip("Debaixo de teto, quanto do som de fora (chuva, trovão, mar, vento) passa: 0,4 = 40%.")]
    [Range(0f, 1f)] public float coveredAmbienceVolume = 0.4f;

    [Tooltip("Debaixo de teto, o som de fora fica abafado: frequência de corte (Hz). Menor = mais abafado.")]
    [Range(300f, 22000f)] public float coveredAmbienceCutoff = 1200f;

    [Tooltip("Chuva no telhado ouvida de dentro (gravação), que entra junto com a chuva de fora abafada.")]
    [Range(0f, 1f)] public float roofRainVolume = 0.3f;

    [Tooltip("Ambiente embaixo d'água (gravação), ao mergulhar.")]
    [Range(0f, 1f)] public float underwaterAmbienceVolume = 0.5f;

    [Tooltip("Chiado do rádio (a fonte fica no rádio, some com a distância).")]
    [Range(0f, 1f)] public float radioVolume = 0.25f;

    [Range(0f, 1f)] public float footstepVolume = 0.45f;

    [Tooltip("Entrar e sair da água: respingo, pingos.")]
    [Range(0f, 1f)] public float waterFxVolume = 0.7f;

    [Tooltip("Vermes atravessando a parede: o som molhado deles (3D, perto da parede).")]
    [Range(0f, 1f)] public float wormsVolume = 0.55f;

    public static event Action Changed;

    private static GameTuning instance;

    public static GameTuning Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameTuning>(ResourceName);
                if (instance == null)
                {
                    // No asset yet: defaults, so nothing ever breaks.
                    instance = CreateInstance<GameTuning>();
                    instance.hideFlags = HideFlags.DontSave;
                }
            }

            return instance;
        }
    }

    public float AmbienceLevel(AmbienceSource.Kind kind)
    {
        switch (kind)
        {
            case AmbienceSource.Kind.Rain: return rainVolume;
            case AmbienceSource.Kind.Thunder: return thunderVolume;
            case AmbienceSource.Kind.Sea: return seaVolume;
            default: return windVolume;
        }
    }

    public float VisibilityFor(string volumeName)
    {
        for (int i = 0; i < exceptions.Length; i++)
        {
            if (exceptions[i].volume == volumeName)
            {
                return exceptions[i].metres;
            }
        }

        return visibility;
    }

    private static readonly int ItemGlowId = Shader.PropertyToID("_ItemGlowStrength");
    private static readonly int ItemGlowDistanceId = Shader.PropertyToID("_ItemGlowDistance");

    // Loaded before the first scene so the shader globals below are set even if nothing asked for it yet.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        Instance.ApplyShaderGlobals();
    }

    private void OnEnable()
    {
        ApplyShaderGlobals();
    }

    // Values read straight by shaders (Horror/ItemGlow).
    private void ApplyShaderGlobals()
    {
        Shader.SetGlobalFloat(ItemGlowId, itemGlow);
        Shader.SetGlobalFloat(ItemGlowDistanceId, Mathf.Max(0.5f, itemGlowDistance));
    }

    private void OnValidate()
    {
        ApplyShaderGlobals();
        Changed?.Invoke();
    }
}
