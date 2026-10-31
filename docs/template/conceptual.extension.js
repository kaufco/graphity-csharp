// The overview stands at the root of the site, where the table of contents of the navigation bar is
// the nearest one, and DocFX then shows no sidebar. Here it shows the namespaces and their types, as
// on the pages of the API, so that the overview leads to them directly.
exports.postTransform = function (model) {
  model._tocRel = 'api/toc.html';
  model._disableToc = false;
  return model;
};
