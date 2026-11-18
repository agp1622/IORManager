# Facturación electrónica (e-CF DGII)

Este documento explica qué se implementó, qué falta verificar contra la DGII antes de emitir
comprobantes reales, y cómo activar el módulo paso a paso.

## Qué se implementó

- **Modelo de datos**: `EcfRange` (rangos de numeración e-NCF autorizados por la DGII por tipo de comprobante) y
  `EcfSubmission` (ciclo de vida del e-CF de cada factura: XML, firma, envío, respuesta de la DGII).
  `Customer.Rnc` para identificar al comprador.
- **Numeración electrónica**: `IEcfNumberGenerator` genera e-NCF (formato `E` + 2 dígitos de tipo +
  10 dígitos de secuencia) únicamente dentro de un rango de numeración e-NCF previamente registrado, y rechaza la
  generación si el rango está agotado o vencido.
- **XML del e-CF**: `IEcfXmlBuilder` arma el XML (`ECF/Encabezado/IdDoc/Emisor/Comprador/Totales`,
  `DetallesItems`, `InformacionReferencia` para notas) siguiendo la estructura documentada por la
  DGII.
- **Firma digital**: `XmlDsigEcfSigner` produce una firma XML-DSig envolvente (RSA-SHA256) más un
  bloque XAdES-BES (`SigningTime`, digest del certificado) usando el certificado `.p12`/`.pfx` del
  contribuyente. Verificado con pruebas automatizadas (firma y verifica correctamente contra el
  propio certificado).
- **Cliente DGII**: `DgiiEcfClient` implementa el flujo de autenticación (semilla → validar semilla →
  token) y el envío/consulta de estado, configurable por ambiente (TesteCF/CerteCF/Producción).
- **Representación Impresa (RI)**: `EcfQrCodeGenerator` arma la URL de verificación "ConsultaTimbre"
  de la DGII y la imprime como código QR en la esquina inferior izquierda del PDF, junto al e-NCF, el
  código de seguridad y la fecha/hora de la firma — ver `QuestPdfFinancialDocumentPdfService`.
- **Contingencia por falta de conectividad**: si el envío a la DGII falla (red/DGII caída), el
  e-CF ya generado y firmado no se pierde — queda en estado `Contingencia` con un reloj de 72 horas
  (`EcfSubmission.ContingencySince`). `EcfContingencyRetryService` reintenta el envío automáticamente
  cada 15 minutos sin volver a firmar el documento (la firma y el código de seguridad deben quedarse
  exactamente igual), y registra una advertencia si una contingencia supera las 72 horas. También se
  puede forzar un reintento manual con `POST /api/invoices/{id}/ecf/retry`, y
  `GET /api/invoices/ecf-contingencies` lista las facturas pendientes.
- **Orquestación**: `EcfService` conecta todo lo anterior; se expone vía `InvoicesController`
  (`/api/invoices/{id}/ecf/emit`, `/ecf/status`, `/ecf/status/refresh`, `/ecf/retry`, `/ecf/xml`,
  `/api/invoices/ecf-ranges`, `/api/invoices/ecf-contingencies`).

## Qué falta verificar antes de producción

Este entorno de desarrollo no tuvo acceso a `dgii.gov.do` ni a credenciales reales de la DGII, así
que lo siguiente está construido según la documentación técnica pública y prácticas estándar de
XAdES, pero **debe verificarse con los documentos oficiales y el ambiente TesteCF antes de emitir
comprobantes reales**:

1. **Rutas exactas de los web services** (`Dgii:Endpoints:*` en `appsettings.json`). Las URLs
   actuales son un patrón razonable (`https://ecf.dgii.gov.do/{ambiente}/...`) pero no están
   confirmadas carácter por carácter contra el documento oficial "Descripción Técnica de
   Facturación Electrónica".
2. **Nombres exactos de campos JSON** en las respuestas de semilla/validarsemilla/recepción/consulta
   (`backend/Services/Dgii/DgiiEcfClient.cs`). El parseo es tolerante a mayúsculas/minúsculas pero
   no cubre variantes desconocidas.
3. **Catálogo completo de campos del XML** por tipo de comprobante, especialmente los tipos con
   secciones propias (E41 compras, E46 exportaciones, E47 pagos al exterior) y los códigos de
   `IndicadorFacturacion` por línea — ver `backend/Services/Dgii/EcfXmlBuilder.cs`.
4. **Perfil exacto de XAdES** que exige la DGII (se implementó XAdES-BES estándar).
5. **Nombres exactos de los parámetros del QR** (`backend/Services/Dgii/EcfQrCodeGenerator.cs`): se
   usó el patrón `RncEmisor/RncComprador/ENCF/FechaEmision/MontoTotal/FechaFirma/CodigoSeguridad`
   sobre `ConsultaTimbre`, y la URL base por ambiente (`Dgii:Endpoints:*:VerificationBaseUrl`) — esto
   también debe verificarse contra "Preguntas Frecuentes e-CF Generales" y escaneando un QR real en
   TesteCF.

Descarga y revisa estos documentos oficiales antes de ir a producción (enlazados desde el
[portal de documentación e-CF de la DGII](https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/documentacionSobreE-CF.aspx)):

- Informe Técnico e-CF v1.0
- Descripción Técnica de Facturación Electrónica
- Formato Comprobante Fiscal Electrónico (e-CF) v1.0
- Proceso de Certificación para ser Emisor Electrónico

## Proceso oficial para ser Emisor Electrónico (según guía DGII, enero 2025)

Esto es el trámite administrativo con la DGII — no se automatiza desde el código, hay que hacerlo
en la Oficina Virtual (OFV) y en el portal de Certificación de Facturación Electrónica.

### Requisitos previos

- RNC inscrito y al día con tus obligaciones tributarias.
- Clave de acceso a la OFV.
- Certificado Digital para Procesos Tributarios, emitido por una entidad certificadora autorizada
  por el INDOTEL, a nombre de la persona que representará a la empresa ante la DGII.
- Formulario **FI-GDF-016** (Solicitud para ser Emisor Electrónico) completado.
- Cumplimiento de la Norma General 06-2018 sobre Comprobantes Fiscales.

### Las 3 etapas

1. **Solicitud** — Envías el formulario FI-GDF-016 desde la OFV. La DGII valida que cumples los
   requisitos y responde por el buzón de mensajería de la OFV con el enlace al portal de
   Certificación de Facturación Electrónica más un usuario y clave para esa siguiente etapa.
2. **Sets de pruebas** — En el portal de Certificación agotas tres bloques de pruebas:
   - *Pruebas de Datos*
   - *Pruebas de Simulación*
   - *Pruebas de Comunicación* — aquí es donde este sistema entra en juego: usan los endpoints
     `/api/invoices/{id}/ecf/emit`, `/ecf/status`, `/ecf/status/refresh` y `/ecf/xml` para demostrar
     que el software genera, firma, envía y recibe el e-CF, el Acuse de Recibo y la Aprobación
     Comercial, y que la Representación Impresa (RI, el PDF) cumple la norma.
   Al pasar las tres, firmas la **Declaración Jurada** (formulario electrónico bajo fe de juramento)
   confirmando que las pruebas se hicieron de forma íntegra.
3. **Certificación** — La DGII te acredita como Emisor Electrónico. Se habilita el menú de
   Facturación Electrónica en la OFV, desde donde solicitas los rangos de numeración e-NCF por cada tipo de
   comprobante que vayas a emitir.

### Obligaciones continuas una vez certificado

- Firmar digitalmente cada e-CF — **ya implementado** (`XmlDsigEcfSigner`).
- Emitir la Representación Impresa (RI) al receptor cuando este no sea electrónico — **ya
  implementado**: el PDF imprime el código QR y el código de seguridad (ver arriba). Pendiente de
  verificar los nombres exactos de los parámetros del QR contra el documento oficial antes de
  producción.
- Recibir válidamente los e-CF de tus proveedores — **no implementado**: lo que construimos cubre
  el rol de emisor (facturar), no el de receptor (recibir facturas electrónicas de terceros). Es un
  flujo aparte si lo necesitas.
- Conservar los e-CF y exhibirlos a la DGII cuando los requiera (Código Tributario).

### Contingencias

- **Sin conectividad (automatizado)**: si el envío falla, el e-CF ya firmado se marca
  `Contingencia` y un job en segundo plano (`EcfContingencyRetryService`) reintenta el envío cada 15
  minutos durante la ventana de 72 horas que exige la DGII, sin volver a firmar el documento. Pasadas
  las 72 horas sin éxito, el sistema sigue reintentando pero además deja una advertencia en los logs
  — en ese punto es una decisión humana, no automática.
- **No se puede emitir el e-CF en absoluto (manual, no implementado)**: la norma permite usar
  comprobantes no electrónicos de respaldo y enviar los e-CF que los reemplazan dentro de 30 días
  calendario (contingencia máxima de 15 días). Esto implica decidir qué secuencia NCF tradicional
  usar y cómo correlacionarla después con el e-CF definitivo — es un proceso de negocio, no solo
  técnico, y queda fuera del alcance de esta primera versión.

### Facturador Gratuito (alternativa de la DGII, no aplica aquí)

La DGII ofrece un facturador propio gratuito para quien no quiera desarrollar/mantener su propio
software y facture como máximo 150 comprobantes al mes. Como ya tienes IORManager, el camino que
construimos (software propio, vía "Solicitud → Pruebas → Certificación" arriba) es el que te
corresponde, no el Facturador Gratuito.

## Pasos para activar el módulo (técnico, una vez tengas acceso al portal de Certificación)

1. **Certificado digital**: tramita un certificado de firma digital con una entidad certificadora
   autorizada por INDOTEL (la DGII no lo emite). Guarda el archivo `.p12`/`.pfx` en el servidor.
2. **Configura `appsettings.json` (o variables de entorno equivalentes)**:
   ```json
   "Dgii": {
     "Environment": "TesteCF",
     "Emisor": { "Rnc": "...", "RazonSocial": "...", "Direccion": "..." },
     "Certificate": { "PfxPath": "/ruta/al/certificado.pfx", "PfxPassword": "..." }
   }
   ```
   No subas el certificado ni la contraseña al repositorio — usa variables de entorno o un secret
   manager en producción.
4. **Registra los rangos de numeración e-NCF** que te otorgue la DGII:
   `PUT /api/invoices/ecf-ranges/{categoryCode}` (ej. `E31`) con
   `{ "rangeStart": 1, "rangeEnd": 5000, "authorizedAt": "2026-08-01", "expiresAt": "2027-12-31" }`.
5. **Prueba en TesteCF** con facturas reales de tu operación antes de solicitar el paso a CerteCF y
   luego producción.
6. Una vez autorizado, cambia `Dgii:Environment` a `Produccion` y actualiza las URLs/rangos con los
   datos de producción.

## Limitaciones conocidas de esta primera versión

- No hay todavía una pantalla para crear notas de crédito/débito (E33/E34) referenciando la factura
  original — el modelo (`EcfReferenciaData`/`InformacionReferencia`) ya lo soporta a nivel de XML,
  pero falta el flujo de negocio en el frontend.
- El envío usa siempre un token nuevo (no cachea el token entre llamadas); es simple y correcto pero
  no el más eficiente si se emiten muchos comprobantes seguidos.
- Los tipos E41/E43-E47 comparten el mismo armado de XML "base"; si alguno requiere secciones
  adicionales específicas (compras, exportación, pagos al exterior) hay que ampliarlas en
  `EcfXmlBuilder` una vez confirmado el formato exacto.
- No se implementa el flujo de recepción de e-CF de terceros (cuando tú eres el comprador), ni la
  contingencia de "no se puede emitir el e-CF en absoluto" (reemplazo por comprobante físico) — ver
  la sección de Contingencias arriba.
- El PDF de la Representación Impresa siempre usa el formato "ConsultaTimbre"; el "Resumen de
  Factura de Consumo Electrónica" (RFCE, el documento periódico para facturas de consumo menores a
  RD$250,000) no está implementado — este sistema envía cada e-CF individualmente vía Recepción
  independientemente del monto.
