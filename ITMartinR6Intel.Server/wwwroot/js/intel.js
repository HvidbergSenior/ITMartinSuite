window.getRelativeClickPosition = function (element, clientX, clientY) {
    const rect = element.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    return {
        x: ((clientX - rect.left) / rect.width) * 100,
        y: ((clientY - rect.top) / rect.height) * 100
    };
};
