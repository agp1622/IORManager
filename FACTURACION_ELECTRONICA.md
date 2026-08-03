# Facturación electrónica (e-CF DGII)

Este documento explica qué se implementó, qué falta verificar contra la DGII antes de emitir
comprobantes reales, y cómo activar el módulo paso a paso.

## Qué se implementó

- **Modelo de datos**: `EcfRange` (rangos RFCE autorizados por la DGII por tipo de comprobante) y
  `EcfSubmission` (ciclo de vida del e-CF de cada factura: XML, firma, envío, respuesta de la DGII).
  `Customer.Rnc` para identificar al comprador.
- **Numeración electrónica**: `IEcfNumberGenerator` genera e-NCF (formato `E` + 2 dígitos de tipo +
  10 dígitos de secuencia) únicamente dentro de un rango RFCE previamente registrado, y rechaza la
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
- **Orquestación**: `EcfService` conecta todo lo anterior; se expone vía `InvoicesController`
  (`/api/invoices/{id}/ecf/emit`, `/ecf/status`, `/ecf/status/refresh`, `/ecf/xml`,
  `/api/invoices/ecf-ranges`).

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

Descarga y revisa estos documentos oficiales antes de ir a producción (enlazados desde el
[portal de documentación e-CF de la DGII](https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/documentacionSobreE-CF.aspx)):

- Informe Técnico e-CF v1.0
- Descripción Técnica de Facturación Electrónica
- Formato Comprobante Fiscal Electrónico (e-CF) v1.0
- Proceso de Certificación para ser Emisor Electrónico

## Pasos para activar el módulo

1. **Trámite ante la DGII**: solicita en la Oficina Virtual (Certificación → Emisor Electrónico) ser
   emisor electrónico. La DGII te dará acceso a los ambientes TesteCF/CerteCF y, tras homologar, un
   rango RFCE por cada tipo de comprobante que vayas a emitir.
2. **Certificado digital**: tramita un certificado de firma digital con una entidad certificadora
   autorizada por INDOTEL (la DGII no lo emite). Guarda el archivo `.p12`/`.pfx` en el servidor.
3. **Configura `appsettings.json` (o variables de entorno equivalentes)**:
   ```json
   "Dgii": {
     "Environment": "TesteCF",
     "Emisor": { "Rnc": "...", "RazonSocial": "...", "Direccion": "..." },
     "Certificate": { "PfxPath": "/ruta/al/certificado.pfx", "PfxPassword": "..." }
   }
   ```
   No subas el certificado ni la contraseña al repositorio — usa variables de entorno o un secret
   manager en producción.
4. **Registra los rangos RFCE** que te otorgue la DGII:
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
