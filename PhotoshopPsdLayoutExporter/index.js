const documentGateway = require("./src/photoshop/photoshopDocumentGateway");
const { createPsdLayoutPanelController } = require("./src/ui/psdLayoutPanelController");

createPsdLayoutPanelController(documentGateway).connect();
